#pragma once
#include <string>
#include "yuspec/ast.h"          // compiler AST
#include "yuspec_rt/world.h"

namespace yuspec_rt {

struct RunConfig {
  int ticks = 200;
  float dt = 0.1f;
  uint32_t seed = 12345;
  bool verbose = false; // print tick trace
};

struct RunResult {
  bool ok = true;
  std::string report;
};

RunResult run_program_v01(const yuspec::Program& program, const RunConfig& cfg);

} // namespace yuspec_rt