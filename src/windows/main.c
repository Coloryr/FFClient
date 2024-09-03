#include "ffclient.h"
#include "socket.h"
#include "ffmpeg.h"

#include <stdio.h>
#include <stdlib.h>
#include <stdbool.h>
#include <windows.h>

/* Called from the main */
int main(int argc, char** argv)
{
    SetDllDirectory("");

    setvbuf(stderr, NULL, _IONBF, 0);

    char** out_args = NULL;
    int out_argc = 0;
    char** in_args = NULL;
    int in_argc = 0;

    //bool enable_output = false;
    bool enable_input = false;

    avdevice_register_all();

    if (argc == 1)
    {
        printf("Must set input or output");
        exit(1);
        return 1;
    }

    for (int i = 0; i < argc; i++)
    {
        if (strcmp("-input", argv[i]) == 0)
        {
            if (i + 1 < argc)
            {
                in_args = &argv[i + 1];
                in_argc = argc - 1;
                enable_input = true;
                break;
            }
        }
        /*else if (strcmp("-output", argv[i]) == 0)
        {
            in_argc = -i;
            if (i + 1 < argc)
            {
                out_args = &argv[i + 1];
                out_argc = argc - i - 1;
                enable_output = true;
                break;
            }
        }*/
    }

    if (in_argc > 0 && in_args != NULL)
    {
        int res = ffclient(in_argc, in_args);
        if (res != 0)
        {
            return res;
        }
    }

    /*if (out_argc > 0 && out_args != NULL)
    {
        int res = ffmpeg(out_argc, out_args);
        if (res != 0)
        {
            return res;
        }
    }*/
    for (;;)
    {
        if (enable_input)
        {
            ffclient_loop();
        }
        /*if (enable_output)
        {
            ffmpeg_loop();
        }*/
    }

    socket_stop();

    return 0;
}