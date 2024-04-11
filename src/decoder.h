#ifndef FFCLIENT_DECODER_H
#define FFCLIENT_DECODER_H

#include <libavcodec/avcodec.h>
#include <libavcodec/packet.h>

#include "packet.h"
#include "frame.h"

typedef struct Decoder
{
    AVPacket *pkt;
    PacketQueue *queue;
    AVCodecContext *avctx;
    int pkt_serial;
    int finished;
    int packet_pending;
    SDL_cond *empty_queue_cond;
    int64_t start_pts;
    AVRational start_pts_tb;
    int64_t next_pts;
    AVRational next_pts_tb;
    SDL_Thread *decoder_tid;
} Decoder;

int decoder_init(Decoder *d, AVCodecContext *avctx, PacketQueue *queue, SDL_cond *empty_queue_cond);
int decoder_decode_frame(Decoder *d, AVFrame *frame);
void decoder_destroy(Decoder *d);
void decoder_abort(Decoder *d, FrameQueue *fq);

#endif