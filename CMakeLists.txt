cmake_minimum_required(VERSION 3.16)
project(yuspec LANGUAGES CXX)

include(FetchContent)

FetchContent_Declare(
  raylib
  GIT_REPOSITORY https://github.com/raysan5/raylib.git
  GIT_TAG 5.0
)

set(BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
FetchContent_MakeAvailable(raylib)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(yuspec_compiler
  compiler/src/lexer.cpp
  compiler/src/parser.cpp
  compiler/src/diagnostic.cpp
  compiler/src/ir.cpp
  compiler/src/json_writer.cpp
  compiler/src/validate.cpp
)
target_include_directories(yuspec_compiler PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
)

add_library(yuspec_runtime
  runtime/src/world.cpp
  runtime/src/entity.cpp
  runtime/src/components.cpp
  runtime/src/runtime.cpp
  runtime/src/rng.cpp
)
target_include_directories(yuspec_runtime PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/runtime/include
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
)

add_executable(yuspec tools/yuspec_cli/main.cpp)
target_link_libraries(yuspec PRIVATE yuspec_compiler yuspec_runtime)

add_executable(yuspec_view engine/raylib_viewer/main.cpp)
target_link_libraries(yuspec_view PRIVATE raylib yuspec_compiler yuspec_runtime)
target_include_directories(yuspec_view PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
  ${CMAKE_CURRENT_SOURCE_DIR}/runtime/include
)

# ════════════════════════════════════════════════════════════════
# YUSPEC v1.0 — Entity-Behavior Programming (EBP) engine
# ════════════════════════════════════════════════════════════════

add_library(yuspec_compiler_v1
  compiler/src/v1_lexer.cpp
  compiler/src/v1_parser.cpp
  compiler/src/v1_sema.cpp
)
target_include_directories(yuspec_compiler_v1 PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
)
target_compile_features(yuspec_compiler_v1 PUBLIC cxx_std_17)

add_library(yuspec_runtime_v1
  runtime/src/v1_sm_runtime.cpp
  runtime/src/v1_interpreter.cpp
)
target_include_directories(yuspec_runtime_v1 PUBLIC
  ${CMAKE_CURRENT_SOURCE_DIR}/runtime/include
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
)
target_compile_features(yuspec_runtime_v1 PUBLIC cxx_std_17)
target_link_libraries(yuspec_runtime_v1 PUBLIC yuspec_compiler_v1)

add_executable(yuspec1 tools/yuspec1_cli/main.cpp)
target_include_directories(yuspec1 PRIVATE
  ${CMAKE_CURRENT_SOURCE_DIR}/compiler/include
  ${CMAKE_CURRENT_SOURCE_DIR}/runtime/include
)
target_link_libraries(yuspec1 PRIVATE yuspec_compiler_v1 yuspec_runtime_v1)
target_compile_features(yuspec1 PRIVATE cxx_std_17)

# Installation rules
install(TARGETS yuspec1 RUNTIME DESTINATION bin)
install(DIRECTORY examples/ DESTINATION share/yuspec/examples)

message(STATUS "YUSPEC v1.0 targets configured: yuspec_compiler_v1, yuspec_runtime_v1, yuspec1")