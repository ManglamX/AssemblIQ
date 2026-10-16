#pragma once

#include <Arduino.h>
#include "config.h"

// Struct representing a confirmed measurement to buffer offline
struct OfflineReading {
    uint64_t seq;
    float value;
    char unit[8];
    char timestamp[32];
    bool isStable;
};

// Thread-safe circular ring buffer for offline measurements (Architecture §12)
class OfflineBuffer {
private:
    OfflineReading _buffer[OFFLINE_BUFFER_CAPACITY];
    int _head;
    int _tail;
    int _count;

public:
    OfflineBuffer() : _head(0), _tail(0), _count(0) {}

    bool push(const OfflineReading& item) {
        if (_count >= OFFLINE_BUFFER_CAPACITY) {
            // Buffer full: overwrite oldest reading (circular overwrite)
            _tail = (_tail + 1) % OFFLINE_BUFFER_CAPACITY;
            _count--;
        }
        _buffer[_head] = item;
        _head = (_head + 1) % OFFLINE_BUFFER_CAPACITY;
        _count++;
        return true;
    }

    bool pop(OfflineReading& item) {
        if (_count == 0) return false;
        item = _buffer[_tail];
        _tail = (_tail + 1) % OFFLINE_BUFFER_CAPACITY;
        _count--;
        return true;
    }

    bool peek(OfflineReading& item) const {
        if (_count == 0) return false;
        item = _buffer[_tail];
        return true;
    }

    int count() const { return _count; }
    bool isEmpty() const { return _count == 0; }
    bool isFull() const { return _count >= OFFLINE_BUFFER_CAPACITY; }
    void clear() { _head = 0; _tail = 0; _count = 0; }
};
