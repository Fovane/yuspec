#pragma once
#include <stdexcept>
#include <string>
#include "yuspec/token.h"

namespace yuspec {

class CompileError : public std::runtime_error {
public:
  explicit CompileError(const std::string& msg) : std::runtime_error(msg) {}
};

struct Diagnostic {
  [[noreturn]] static void error_at(const Token& tok, const std::string& message);
};

} // namespace yuspec