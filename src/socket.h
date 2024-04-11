#ifndef FFCLIENT_SOCKET_H
#define FFCLIENT_SOCKET_H

#include <inttypes.h>

typedef union 
{
    int i32;
    uint8_t u8[4];
} I32U8;

extern uint8_t socket_send;
extern uint8_t socket_conn;

extern int need_exit;

void init_socket(char* addr);
void socket_send_image_size(char* name, int width, int height);
void socket_send_image(void* ptr, int size);
void socket_stop();

#endif