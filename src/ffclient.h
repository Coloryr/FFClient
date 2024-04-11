#ifndef FFCLIENT_H
#define FFCLIENT_H

int ffclient();

extern char* input_filename;
extern char* mem_name;
extern char* hw_name;

extern int img_max_width;
extern int img_max_height;
extern int disable_audio;
extern int nobuffer;

#endif