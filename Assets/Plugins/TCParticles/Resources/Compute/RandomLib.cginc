#if !defined(TC_RANDOM_LIB)
#define TC_RANDOM_LIB

#define PI2 6.28318530717
uint rng_state;

//Random number system
uint WangHash(uint id, uint time) {
	//wang hash, to randomize seed and make sure xorshift isn't coherent.
	rng_state = id + time;
	rng_state = (rng_state ^ 61) ^ (rng_state >> 16);
	rng_state *= 9;
	rng_state = rng_state ^ (rng_state >> 4);
	rng_state *= 0x27d4eb2d;
	rng_state = rng_state ^ (rng_state >> 15);
	return rng_state;
}

uint NextXor() {
	rng_state ^= (rng_state << 13);
	rng_state ^= (rng_state >> 17);
	rng_state ^= (rng_state << 5);
	return rng_state;
}
//end internal

//public Random API
float FirstRandom(uint id, uint time) {
	return WangHash(id, time) * 1.0f / 4294967295.0f;
}

float Random() {
	return NextXor() * 1.0f / 4294967295.0f;
}

float3 RandomInUnitSphere() {
	float3 rand;

	[unroll]
	for (int i = 0; i < 7; ++i) {
		rand = float3(Random() * 2.0f - 1.0f,
			Random() * 2.0f - 1.0f,
			Random() * 2.0f - 1.0f);

		if (dot(rand, rand) < 1) {
			return rand;
		}
	}

	return normalize(rand);
}

float3 RandomInUnitCircle() {
	float3 rand;

	[unroll]
	for (int i = 0; i < 7; ++i) {
		rand = float3(Random() * 2.0f - 1.0f,
			Random() * 2.0f - 1.0f,
			0);

		if (dot(rand, rand) < 1) {
			return rand;
		}
	}

	return normalize(rand);
}

float3 RandomOnUnitSphere() {
	float u = Random();
	float theta = Random() * PI2;
	float sq = sqrt(1 - u * u);

	return float3(sq * cos(theta),
		sq * sin(theta), u);
}

float RandomRange(float minVal, float maxVal) {
	return lerp(minVal, maxVal, Random());
}

int RandomRangeInt(int minVal, int maxVal) {
	return minVal + NextXor() % (maxVal - minVal);
}

uint RandomRangeUInt(uint minVal, uint maxVal) {
	return minVal + NextXor() % (maxVal - minVal);
}

uint RandomRangeUInt(uint max) {
	return NextXor() % max;
}

#endif