#pragma once
#include "yuspec/ast.h"

namespace yuspec {

struct ValidationResult {
  bool ok = true;
  std::string report; // insan-okur
};

ValidationResult validate_v01(const Program& program);

} // namespace yuspec