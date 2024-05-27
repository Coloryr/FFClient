target_sources(${APP_NAME}
PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/ffmpeg.c
)

target_include_directories(${APP_NAME} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})