#include "../socket.h"

#include <errno.h>

#include <libavutil/log.h>

#include <winsock2.h>
#include <ws2tcpip.h>
#include <stdio.h>
#include <windows.h>

#include <SDL.h>

#pragma comment(lib, "ws2_32.lib")

SOCKET socket_fd = INVALID_SOCKET;
struct sockaddr_in socket_addr;
uint8_t socket_conn = 0;
uint8_t socket_send = 0;

void *shm = NULL;
HANDLE handel = NULL;

uint8_t temp[256];

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
        }
        else if (size == SOCKET_ERROR || size == 0)
        {
            exit(0);
        }
    }
}

void init_socket(char *addr)
{
    WORD sockVersion = MAKEWORD(2, 2);
    WSADATA data;
    if (WSAStartup(sockVersion, &data) != 0)
    {
        return 0;
    }

    int port = atoi(addr);

    socket_fd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (socket_fd < 0)
    {
        av_log(NULL, AV_LOG_ERROR, "socket create fail");
    }

    av_log(NULL, AV_LOG_INFO, "create sockfd[%d] ok!\n", socket_fd);

    socket_addr.sin_family = AF_INET;
    socket_addr.sin_port = htons(port);
    socket_addr.sin_addr.S_un.S_addr = inet_addr("127.0.0.1");

    if (connect(socket_fd, (struct sockaddr *)&socket_addr, sizeof(socket_addr)) == SOCKET_ERROR)
    {
        socket_conn = 0;
        av_log(NULL, AV_LOG_INFO, "Connect to socket server on \"%d\" failure:%s\n", port, strerror(errno));
        return;
    }

    socket_conn = 1;
    av_log(NULL, AV_LOG_INFO, "connect socket \"%d\" ok!\n", port);

    SDL_CreateThread(socket_read, "socket_read", NULL);
}

void socket_stop()
{
    if (socket_fd != INVALID_SOCKET)
    {
        closesocket(socket_fd);
        socket_fd = INVALID_SOCKET;
    }
    WSACleanup();

    if (shm != NULL)
    {
        UnmapViewOfFile(shm);
        shm = NULL;
    }
    if (handel != NULL)
    {
        CloseHandle(handel);
        handel = NULL;
    }
}

void socket_send_image_size(char* name, int width, int height)
{
    // 创建共享内存

    handel = CreateFileMapping(INVALID_HANDLE_VALUE,
        NULL, PAGE_READWRITE, 0, width * height * 4, name);

    if (handel == NULL)
    {
        fprintf(stderr, "share mem create failed\n");
        exit(EXIT_FAILURE);
    }

    // 将共享内存连接到当前的进程地址空间
    shm = MapViewOfFile(handel, FILE_MAP_ALL_ACCESS, 0, 0, 0);
    if (shm == (void*)-1)
    {
        fprintf(stderr, "share mem link failed\n");
        exit(EXIT_FAILURE);
    }

    printf("Memory attched at %X\n", (int)shm);

    uint8_t temp[32] = { 0 };
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

    cov.i32 = 0;
    temp[10] = cov.u8[0];
    temp[11] = cov.u8[1];
    temp[12] = cov.u8[2];
    temp[13] = cov.u8[3];

    if (send(socket_fd, temp, 16, 0) == SOCKET_ERROR)
    {
        exit(0);
    }
}

void socket_send_image(void *ptr, int size)
{
    memcpy(shm, ptr, size);
}