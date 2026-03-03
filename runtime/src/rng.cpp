#include "yuspec_rt/rng.h"

namespace yuspec_rt {

Rng::Rng(uint32_t seed) : mt_(seed) {}

int Rng::next_int(int min_inclusive, int max_inclusive) {
  std::uniform_int_distribution<int> dist(min_inclusive, max_inclusive);
  return dist(mt_);
}

} // namespace yuspec_rt