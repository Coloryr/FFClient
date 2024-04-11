#ifndef FFCLIENT_PACKET_H
#define FFCLIENT_PACKET_H

#include <libavcodec/packet.h>
#include <libavutil/fifo.h>
#include <libavformat/avformat.h>

#ifdef _WIN64 || _WIN32
#include <SDL.h>
#else
#include <SDL2/SDL.h>
#endif // _WIN64 || _WIN32

#define MIN_FRAMES 25

typedef struct MyAVPacketList
{
    AVPacket *pkt;
    int serial;
} MyAVPacketList;

typedef struct PacketQueue
{
    AVFifo *pkt_list;
    int nb_packets;
    int size;
    int64_t duration;
    int abort_request;
    int serial;
    SDL_mutex *mutex;
    SDL_cond *cond;
} PacketQueue;

int packet_queue_put_private(PacketQueue *q, AVPacket *pkt);
int packet_queue_put(PacketQueue *q, AVPacket *pkt);
int packet_queue_put_nullpacket(PacketQueue *q, AVPacket *pkt, int stream_index);
int packet_queue_init(PacketQueue *q);
void packet_queue_flush(PacketQueue *q);
void packet_queue_destroy(PacketQueue *q);
void packet_queue_abort(PacketQueue *q);
void packet_queue_start(PacketQueue *q);
int packet_queue_get(PacketQueue *q, AVPacket *pkt, int block, int *serial);
int stream_has_enough_packets(AVStream *st, int stream_id, PacketQueue *queue);

#endif