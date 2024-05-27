#include "ffmpeg.h"
#include <libavformat/avformat.h>
#include <libavcodec/avcodec.h>
#include <libswscale/swscale.h>
#include <libavutil/imgutils.h>

static int avError(int errNum) 
{
    char buf[1024];
    //获取错误信息
    av_strerror(errNum, buf, sizeof(buf));
    printf(" failed! %s\n", buf);;
    return -1;
}

static int open_video(char* video, char* input, char* frame, char* size, int* out_stream,
    AVCodecContext** out_codec_ctx, AVFrame* yuvFrame, struct SwsContext** sws_ctx, AVFormatContext** out_ctx)
{
    AVInputFormat* video_format = av_find_input_format(video);
    if (video_format == NULL)
    {
        printf("can't find %s\n", video);
        return -1;
    }
    AVDictionary* video_option = NULL;
    av_dict_set(&video_option, "video_size", size, 0);
    av_dict_set(&video_option, "framerate", frame, 0);
    int ret = avformat_open_input(out_ctx, input, video_format, video_option);
    if (ret < 0)
    {
        return avError(ret);
    }
    ret = avformat_find_stream_info(*out_ctx, 0);
    if (ret != 0)
    {
        return avError(ret);
    }
    av_dump_format(out_ctx, 0, input, 0);
    
    for (size_t i = 0; i < (*out_ctx)->nb_streams; i++)
    {
        if ((*out_ctx)->streams[i]->codecpar->codec_type == AVMEDIA_TYPE_VIDEO)
        {
            *out_stream = i;
            break;
        }
    }
    if (*out_stream == -1)
    {
        printf("Cannot find video stream in file.\n");
        return -1;
    }

    AVCodecParameters* inVideoCodecPara = (*out_ctx)->streams[*out_stream]->codecpar;

    AVCodec* video_codec = avcodec_find_decoder(inVideoCodecPara->codec_id);
    if (video_codec == NULL)
    {
        printf("can't find decoder\n");
        return -1;
    }

    if (!(*out_codec_ctx = avcodec_alloc_context3(video_codec)))
    {
        printf("Cannot alloc valid decode codec context.\n");
        return -1;
    }

    if (avcodec_parameters_to_context(*out_codec_ctx, inVideoCodecPara) < 0)
    {
        printf("Cannot initialize parameters.\n");
        return -1;
    }

    if (avcodec_open2(*out_codec_ctx, video_codec, NULL) < 0)
    {
        printf("Cannot open codec.\n");
        return -1;
    }

    *sws_ctx = sws_getContext((*out_codec_ctx)->width,
        (*out_codec_ctx)->height,
        (*out_codec_ctx)->pix_fmt,
        (*out_codec_ctx)->width,
        (*out_codec_ctx)->height,
        AV_PIX_FMT_YUV420P,
        SWS_BICUBIC,
        NULL, NULL, NULL);

    int numBytes = av_image_get_buffer_size(AV_PIX_FMT_YUV420P,
        (*out_codec_ctx)->width,
        (*out_codec_ctx)->height, 1);
    uint8_t* out_buffer = (unsigned char*)av_malloc(numBytes * sizeof(unsigned char));

    ret = av_image_fill_arrays(yuvFrame->data,
        yuvFrame->linesize,
        out_buffer,
        AV_PIX_FMT_YUV420P,
        (*out_codec_ctx)->width,
        (*out_codec_ctx)->height,
        1);
    if (ret != 0)
    {
        return avError(ret);
    }
    return ret;
}

static int open_out_video(char* send, char* send_dir,char* frame, char* bitrate,char* codec, int width, 
    int height, AVFormatContext* out_ctx, AVStream** out_video_stream, AVCodecContext** video_out_codec_ctx)
{
    int video_frame = atoi(frame);
    int bit = atoi(bitrate);

    AVOutputFormat* outFmt = out_ctx->oformat;

    //打开视频编码器
    AVCodec* video_out_codec = avcodec_find_encoder_by_name(codec);
    if (video_out_codec == NULL)
    {
        printf("Cannot find encoder %s\n", codec);
        return -1;
    }

    //创建h264视频流，并设置参数
    *out_video_stream = avformat_new_stream(out_ctx, video_out_codec);
    if (out_video_stream == NULL)
    {
        printf("create new video stream fialed.\n");
        return -1;
    }
    (*out_video_stream)->time_base.den = video_frame;
    (*out_video_stream)->time_base.num = 1;

    //设置编码器内容
    *video_out_codec_ctx = avcodec_alloc_context3(video_out_codec);
    avcodec_parameters_to_context(*video_out_codec_ctx, out_ctx->streams[(*out_video_stream)->index]->codecpar);
    if (*video_out_codec_ctx == NULL)
    {
        printf("Cannot alloc output codec content.\n");
        return -1;
    }
    (*video_out_codec_ctx)->codec_id = outFmt->video_codec;
    (*video_out_codec_ctx)->codec_type = AVMEDIA_TYPE_VIDEO;
    (*video_out_codec_ctx)->pix_fmt = AV_PIX_FMT_YUV420P;
    (*video_out_codec_ctx)->width = width;
    (*video_out_codec_ctx)->height = height;
    (*video_out_codec_ctx)->time_base.num = 1;
    (*video_out_codec_ctx)->time_base.den = video_frame;
    (*video_out_codec_ctx)->bit_rate = bit;
    (*video_out_codec_ctx)->gop_size = video_frame;

    if ((*video_out_codec_ctx)->codec_id == AV_CODEC_ID_H264)
    {
        (*video_out_codec_ctx)->qmin = 10;
        (*video_out_codec_ctx)->qmax = 51;
        (*video_out_codec_ctx)->qcompress = (float)0.6;
    }

    //打开编码器
    int ret = avcodec_open2(*video_out_codec_ctx, video_out_codec, NULL);
    if (ret < 0)
    {
        return avError(ret);
    }

    return 0;
}

AVFormatContext* video_fmt_ctx, * audio_fmt_ctx;
AVPacket* read_packet;
AVFrame* src_frame;
AVFrame* yuv_frame;
AVPacket* out_packet;
int video_in_stream, audio_in_stream;
AVStream* video_out_stream, audio_out_stream;
AVCodecContext* video_codec_ctx, * audio_codec_ctx;
AVCodecContext* video_out_codec_ctx, * audio_out_codec_ctx;
struct SwsContext* video_sws_ctx, * audio_sws_ctx;
AVFormatContext* out_fmt_ctx;

int ffmpeg(int argc, char** argv)
{
    char* video = "v4l2";
    char* video_input = "/dev/video0";
    char* video_frame = "30";
    char* video_size = "640x480";
    char* video_codec = "h264";
    char* video_bitrate = "2000000";

    char* audio = "alsa";
    char* audio_input = "plughw:2";
    char* audio_format = "pcm_alaw";

    char* send = "rtsp";
    char* send_dir = "rtsp://localhost:554/video1";

    for (int a = 0; a < argc; a++)
    {
        if (strcmp("-video", argv[a]) == 0)
        {
            if (a + 6 >= argc)
            {
                return -1;
            }
            video = argv[a + 1];
            video_input = argv[a + 2];
            video_frame = argv[a + 3];
            video_size = argv[a + 4];
            video_codec = argv[a + 5];
            video_bitrate = argv[a + 6];
            a += 6;
        }
        else if (strcmp("-audio", argv[a]) == 0)
        {
            if (a + 3 >= argc)
            {
                return -1;
            }
            audio = argv[a + 1];
            audio_input = argv[a + 2];
            audio_format = argv[a + 3];
            a += 3;
        }
        else if (strcmp("-send", argv[a]) == 0)
        {
            if (a + 2 >= argc)
            {
                return -1;
            }
            send = argv[a + 1];
            send_dir = argv[a + 2];
            a += 2;
        }
    }

    read_packet = av_packet_alloc();
    src_frame = av_frame_alloc();
    yuv_frame = av_frame_alloc();
    out_packet = av_packet_alloc();

    //打开视频输入
    int ret = open_video(video, video_input, video_frame, video_size,
        video_codec, video_bitrate, &video_in_stream,
        &video_codec_ctx, yuv_frame, &video_sws_ctx, &video_fmt_ctx);
    if (ret != 0)
    {
        printf("open input video error");
        return -1;
    }

    //创建输出上下文
    ret = avformat_alloc_output_context2(&out_fmt_ctx, NULL, send, send_dir);
    if (ret < 0)
    {
        return avError(ret);
    }

    ret = open_out_video(send, send_dir, video_frame, video_bitrate, video_codec,
        video_codec_ctx->width, video_codec_ctx->height, out_fmt_ctx, 
        &video_out_stream, &video_out_codec_ctx);
    if (ret != 0)
    {
        printf("open input video output error");
        return -1;
    }

    //打开io
    ret = avio_open(&out_fmt_ctx->pb, send_dir, AVIO_FLAG_WRITE);
    if (ret < 0)
    {
        avError(ret);
        return -1;
    }

    yuv_frame->format = video_out_codec_ctx->pix_fmt;
    yuv_frame->width = video_out_codec_ctx->width;
    yuv_frame->height = video_out_codec_ctx->height;

    AVOutputFormat* out_fmt = out_fmt_ctx->oformat;

    ret = avformat_write_header(out_fmt, NULL);
    if (ret < 0)
    {
        avError(ret);
        return -1;
    }
}

void ffmpeg_loop()
{
    while (av_read_frame(video_fmt_ctx, read_packet) > 0)
    {
        if (read_packet->stream_index != video_in_stream)
        {
            continue;
        }

        if (avcodec_send_packet(video_codec_ctx, read_packet) >= 0)
        {
            int ret;
            while ((ret = avcodec_receive_frame(video_codec_ctx, src_frame)) >= 0)
            {
                if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
                    return -1;
                else if (ret < 0)
                {
                    fprintf(stderr, "Error during decoding\n");
                    exit(1);
                }
                sws_scale(video_sws_ctx,
                    src_frame->data, src_frame->linesize,
                    0, video_codec_ctx->height,
                    yuv_frame->data, yuv_frame->linesize);

                yuv_frame->pts = src_frame->pts;
                //encode
                if (avcodec_send_frame(video_out_codec_ctx, yuv_frame) >= 0)
                {
                    if (avcodec_receive_packet(video_out_codec_ctx, out_packet) >= 0)
                    {
                        printf("encode one frame.\n");
                        out_packet->stream_index = video_out_stream->index;
                        av_packet_rescale_ts(out_packet, video_out_codec_ctx->time_base,
                            video_out_stream->time_base);
                        out_packet->pos = -1;
                        av_interleaved_write_frame(out_fmt_ctx, out_packet);
                        av_packet_unref(out_packet);
                    }
                }
            }
        }
        av_packet_unref(read_packet);
    }
}