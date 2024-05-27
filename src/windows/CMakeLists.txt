cmake_minimum_required(VERSION 3.12.0)

set(APP_NAME ffclient)

project(${APP_NAME})

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}/bin/${APP_NAME})

set(SRC_DIR ${CMAKE_CURRENT_SOURCE_DIR}/..)

add_executable(${APP_NAME})

add_subdirectory(${SRC_DIR} ${SRC_DIR}/build)

target_sources(
    ${APP_NAME}
    PRIVATE
    socket.c main.c
)

set(FFMPEG_PATH "E:/environment/ffmpeg-n6.1-latest-win64-gpl-shared-6.1")
set(SDL_PATH "E:/environment/SDL2")

target_include_directories(
    ${APP_NAME} PUBLIC
    ${FFMPEG_PATH}/include
    ${SDL_PATH}/include
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
