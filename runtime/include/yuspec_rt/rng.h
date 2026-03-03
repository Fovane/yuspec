#pragma once
#include <random>
#include <cstdint>

namespace yuspec_rt {

class Rng {
public:
  explicit Rng(uint32_t seed);
  int next_int(int min_inclusive, int max_inclusive);

private:
  std::mt19937 mt_;
};

} // namespace yuspec_rt