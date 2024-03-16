cmake_minimum_required(VERSION 3.25)
project(TreeHouse.Othertransport LANGUAGES CXX)

add_subdirectory(ThirdParty/minhook)

file(GLOB_RECURSE SOURCE CONFIGURE_DEPENDS src/*.cpp)

add_library(TreeHouse.Othertransport MODULE ${SOURCE} Othertransport.def)

if(MSVC)
    set_target_properties(
        minhook TreeHouse.Othertransport PROPERTIES
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>"
    )
    target_compile_options(TreeHouse.Othertransport PRIVATE /Zc:preprocessor)
endif()

target_precompile_headers(TreeHouse.Othertransport PRIVATE src/pch.h)

set_target_properties(
    TreeHouse.Othertransport PROPERTIES
    CXX_STANDARD 20
    CXX_EXTENSIONS OFF
)

target_include_directories(
    TreeHouse.Othertransport SYSTEM PRIVATE
    ThirdParty/asio/asio/include
    ThirdParty/readerwriterqueue
)
target_link_libraries(TreeHouse.Othertransport PRIVATE minhook)