target_sources(${APP_NAME}
PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/clock.c
    ${CMAKE_CURRENT_SOURCE_DIR}/decoder.c
    ${CMAKE_CURRENT_SOURCE_DIR}/ffclient.c
    ${CMAKE_CURRENT_SOURCE_DIR}/frame.c
    ${CMAKE_CURRENT_SOURCE_DIR}/packet.c
    ${CMAKE_CURRENT_SOURCE_DIR}/utils.c
    ${CMAKE_CURRENT_SOURCE_DIR}/video.c
)

target_include_directories(${APP_NAME} PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})