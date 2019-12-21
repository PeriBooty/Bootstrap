cmake_minimum_required (VERSION 3.8)
project (Bootstrap)

set (CMAKE_CXX_STANDARD 17)

include_directory (src)

add_executable (Bootstrap src/main.cpp src/termcolor.hpp)
target_link_libraries (Bootstrap archive)