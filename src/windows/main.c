#include "../ffclient.h"
#include "../socket.h"

#include <stdio.h>
#include <stdlib.h>
#include <windows.h>

/* Called from the main */
int main(int argc, char** argv)
{
    SetDllDirectory("");

    if (argc == 1)
    {
        input_filename = "C:\\Users\\user\\Desktop\\movie.mp4";
        img_max_width = 0;
        img_max_height = 0;
        init_socket("666");
        mem_name = "1234";
    }
    else if (argc < 6)
    {
        printf("Arg size is not 5\n");
        return 1;
    }
    else
    {
        input_filename = argv[1];
        mem_name = argv[5];

        img_max_width = atoi(argv[2]);
        img_max_height = atoi(argv[3]);

        init_socket(argv[4]);

        if (argc > 6)
        {
            for (int i = 6; i < argc; i++)
            {
                if (strcmp("disable_audio", argv[i]) == 0)
                {
                    disable_audio = 1;
                }
                else if (strcmp("nobuffer", argv[i]) == 0)
                {
                    nobuffer = 1;
                }
                else
                {
                    hw_name = argv[i];
                }
            }
        }
    }

    int res = ffclient();
    socket_stop();

    return res;
}