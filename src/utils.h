#ifndef FFCLIENT_UTILS_H
#define FFCLIENT_UTILS_H

#include <libavformat/avformat.h>
#include <libavutil/rational.h>

#ifdef _WIN64 || _WIN32
#include <SDL.h>
#else
#include <SDL2/SDL.h>
#endif // _WIN64 || _WIN32

int compute_mod(int a, int b);
int cmp_audio_fmts(enum AVSampleFormat fmt1, int64_t channel_count1,
                   enum AVSampleFormat fmt2, int64_t channel_count2);
void calculate_display_rect(SDL_Rect *rect, int scr_width, int scr_height,
                            int pic_width, int pic_height, AVRational pic_sar);
void sigterm_handler(int sig);

#endif