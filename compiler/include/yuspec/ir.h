#pragma once
#include <string>
#include "yuspec/ast.h"

namespace yuspec {

// We keep IR as a string (JSON) for v0.1.
// Later: structured IR types + schema validation.
struct IR {
  std::string json;
};

IR build_ir_v01(const Program& program);

// Human-friendly trace to preserve “I understand this”.
std::string build_trace(const Program& program);

} // namespace yuspec