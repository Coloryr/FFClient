#include "../socket.h"

#include <errno.h>

#include <libavutil/log.h>

#include <sys/un.h>
#include <sys/socket.h>
#include <sys/shm.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <stdio.h>

#include <SDL2/SDL.h>

char *unix_addr;
int socket_fd = 0;
struct sockaddr_un socket_addr;
uint8_t socket_conn = 0;
uint8_t socket_send = 0;

uint8_t temp[256];

void *shm = NULL;
int shmid = -1;

int socket_read(void *arg)
{
    for (;;)
    {
        int size = recv(socket_fd, temp, 256, 0);
        if (size == 4)
        {
            if (temp[0] == 0xcf && temp[1] == 0x1f && temp[2] == 0xe4 && temp[3] == 0x98)
            {
                av_log(NULL, AV_LOG_INFO, "start send image\n");
                socket_send = 1;
            }
            else if (temp[0] == 0xcf && temp[1] == 0x1f && temp[2] == 0x98 && temp[3] == 0x31)
            {
                need_exit = 1;
                break;
            }
        }
        else if (size <= 0)
        {
            need_exit = 1;
            break;
        }
    }
}

void init_socket(char *addr)
{
    unix_addr = addr;
    socklen_t addrlen = sizeof(socket_addr);
    socket_fd = socket(AF_UNIX, SOCK_STREAM, 0);
    if (socket_fd < 0)
    {
        av_log(NULL, AV_LOG_ERROR, "socket create fail");
    }

    av_log(NULL, AV_LOG_INFO, "create sockfd[%d] ok!\n", socket_fd);

    memset(&socket_addr, 0, addrlen);

    socket_addr.sun_family = AF_UNIX;
    strncpy(socket_addr.sun_path, unix_addr, sizeof(socket_addr.sun_path) - 1);

    if (connect(socket_fd, (struct sockaddr *)&socket_addr, addrlen) < 0)
    {
        socket_conn = 0;
        av_log(NULL, AV_LOG_INFO, "Connect to unix domain socket server on \"%s\" failure:%s\n", unix_addr, strerror(errno));
        return;
    }

    socket_conn = 1;
    av_log(NULL, AV_LOG_INFO, "connect unix domain socket \"%s\" ok!\n", unix_addr);

    SDL_CreateThread(socket_read, "socket_read", NULL);
}

void socket_send_image_size(char *name, int width, int height)
{
    key_t key = atoi(name);
    // 创建共享内存
    shmid = shmget(key, width * height * 4, 0666 | IPC_CREAT);
    if (shmid == -1)
    {
        fprintf(stderr, "shmget failed\n");
        exit(EXIT_FAILURE);
    }

    // 将共享内存连接到当前的进程地址空间
    shm = shmat(shmid, (void *)0, 0);
    if (shm == (void *)-1)
    {
        fprintf(stderr, "shmat failed\n");
        exit(EXIT_FAILURE);
    }

    printf("Memory attched at %p\n", shm);

    uint8_t temp[32] = {0};
    I32U8 cov;

    temp[0] = 0xff;
    temp[1] = 0x54;

    cov.i32 = width;
    temp[2] = cov.u8[0];
    temp[3] = cov.u8[1];
    temp[4] = cov.u8[2];
    temp[5] = cov.u8[3];

    cov.i32 = height;
    temp[6] = cov.u8[0];
    temp[7] = cov.u8[1];
    temp[8] = cov.u8[2];
    temp[9] = cov.u8[3];

    cov.i32 = shmid;
    temp[10] = cov.u8[0];
    temp[11] = cov.u8[1];
    temp[12] = cov.u8[2];
    temp[13] = cov.u8[3];

    if (send(socket_fd, temp, 16, 0) <= 0)
    {
        need_exit = 1;
        break;
    }
}

void socket_stop()
{
    if (socket_fd != 0)
    {
        close(socket_fd);
        socket_fd = 0;
    }
    if (shm != NULL)
    {
        shmdt(shm);
        shm = NULL;
    }
    if (shmid != -1)
    {
        shmctl(shmid, IPC_RMID, NULL);
        shmid = -1;
    }
}

void socket_send_image(void *ptr, int size)
{
    memcpy(shm, ptr, size);
}