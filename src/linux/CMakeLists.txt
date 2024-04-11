cmake_minimum_required(VERSION 3.0.0)
project(ffclient VERSION 0.1.0 LANGUAGES C)

add_executable(ffclient ../packet.c ../decoder.c ../frame.c 
../clock.c ../video.c ../utils.c ../ffclient.c socket.c main.c)

target_link_libraries(${PROJECT_NAME} 
    avcodec
    avdevice
    avfilter
    avformat
    avutil
    swscale
    swresample
    SDL2
    bsd
    m
)

set(CPACK_PROJECT_NAME ${PROJECT_NAME})
set(CPACK_PROJECT_VERSION ${PROJECT_VERSION})
include(CPack)
