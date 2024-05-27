add_subdirectory(ffclient)
add_subdirectory(ffmpeg)

target_include_directories(${APP_NAME} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})