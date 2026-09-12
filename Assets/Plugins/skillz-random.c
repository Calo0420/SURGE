#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include "mt19937ar.h"

// only seedRandomWithArray() and getRandomFloat() is used in the Skillz Unity SDK
void seedRandom(const char *seed);
void seedRandomWithArray(unsigned int *numbers, int count);
int getRandomNumber();
unsigned int getRandomNumberWithMin(int min, int max);
float getRandomFloat();


void seedRandomWithArray(unsigned int *numbers, int count) {
    const int kMaxNumberSeeds = 624;
    unsigned long seeds[kMaxNumberSeeds];
    
    int counter = 0;
    for (int i = 0; i < count; i++) {
        seeds[counter++] = numbers[i];
    }

    init_by_array(seeds, counter);
}

int getRandomNumber() {
    return genrand_int31();
}

unsigned int getRandomNumberWithMin(int min, int max) {
    int base_random = getRandomNumber();

    if (RAND_MAX == base_random) return getRandomNumberWithMin(min, max);

    int range = max - min;
    int remainder = RAND_MAX % range;
    int bucket = RAND_MAX / range;

    if (base_random < RAND_MAX - remainder) {
        return min + base_random / bucket;
    } else {
        return getRandomNumberWithMin(min, max);
    }
}


float getRandomFloat() {
    const int randomIntBitLength = 24;
    const uint32_t nextRandomInt = genrand_int32() >> (32 - randomIntBitLength);
    // Now, match Java's implementation for converting
    // the int to a float.
    // See: https://docs.oracle.com/javase/7/docs/api/java/util/Random.html
    const float nextRandomFloat = nextRandomInt / ((float)(1 << randomIntBitLength));
    return nextRandomFloat;
}
