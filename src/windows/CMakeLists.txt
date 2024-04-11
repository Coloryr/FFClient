cmake_minimum_required(VERSION 3.12.0)
project(ffclient VERSION 0.1.0 LANGUAGES C)

add_executable(ffclient ../packet.c ../decoder.c ../frame.c 
../clock.c ../video.c ../utils.c ../ffclient.c socket.c main.c)

set(FFMPEG_PATH "C:/Users/user/Desktop/ffmpeg-master-latest-win64-gpl-shared")
set(SDL_PATH "C:/Users/user/Desktop/SDL2-2.30.2")

include_directories(
    "${FFMPEG_PATH}/include"
    "${SDL_PATH}/include"
)

target_link_libraries(${PROJECT_NAME} 
    ${FFMPEG_PATH}/lib/avcodec.lib
    ${FFMPEG_PATH}/lib/avdevice.lib
    ${FFMPEG_PATH}/lib/avfilter.lib
    ${FFMPEG_PATH}/lib/avformat.lib
    ${FFMPEG_PATH}/lib/avutil.lib
    ${FFMPEG_PATH}/lib/swscale.lib
    ${FFMPEG_PATH}/lib/swresample.lib
    ${SDL_PATH}/lib/x64/SDL2.lib
)

set(CPACK_PROJECT_NAME ${PROJECT_NAME})
set(CPACK_PROJECT_VERSION ${PROJECT_VERSION})
include(CPack)
