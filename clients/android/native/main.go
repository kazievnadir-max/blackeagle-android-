// C ABI adapter. The upstream AmneziaWG engine is imported without changes.
package main

/*
#include <stdint.h>
typedef int (*protect_fn)(int);
static int protect_socket(protect_fn fn, int fd) { return fn(fd); }
*/
import "C"

import (
	"crypto/rand"
	"errors"
	"strconv"
	"strings"
	"sync"
	"unsafe"

	"github.com/amnezia-vpn/amneziawg-go/v3/conn"
	"github.com/amnezia-vpn/amneziawg-go/v3/device"
	"github.com/amnezia-vpn/amneziawg-go/v3/tun"
	"golang.org/x/crypto/curve25519"
	"golang.org/x/sys/unix"
)

// Protect every newly opened UDP socket, including after network rebinding.
type protectedBind struct {
	conn.Bind
	protect C.protect_fn
}

func (b *protectedBind) Open(port uint16) ([]conn.ReceiveFunc, uint16, error) {
	receive, actual, err := b.Bind.Open(port)
	if err != nil {
		return nil, 0, err
	}
	peek, ok := b.Bind.(conn.PeekLookAtSocketFd)
	if !ok {
		b.Close()
		return nil, 0, errors.New("socket protection unavailable")
	}
	fd4, err4 := peek.PeekLookAtSocketFd4()
	fd6, err6 := peek.PeekLookAtSocketFd6()
	if (err4 == nil && C.protect_socket(b.protect, C.int(fd4)) != 1) ||
		(err6 == nil && C.protect_socket(b.protect, C.int(fd6)) != 1) || (err4 != nil && err6 != nil) {
		b.Close()
		return nil, 0, errors.New("socket protection failed")
	}
	return receive, actual, nil
}

var mutex sync.Mutex
var active *device.Device

//export be_awg_keys
func be_awg_keys(private, public *C.uchar) C.int {
	key := make([]byte, 32)
	if _, err := rand.Read(key); err != nil {
		return -1
	}
	key[0] &= 248
	key[31] &= 127
	key[31] |= 64
	pub, err := curve25519.X25519(key, curve25519.Basepoint)
	if err != nil {
		return -1
	}
	copy(unsafe.Slice((*byte)(unsafe.Pointer(private)), 32), key)
	copy(unsafe.Slice((*byte)(unsafe.Pointer(public)), 32), pub)
	clear(key)
	return 0
}

//export be_awg_start
func be_awg_start(fd C.int, settings *C.char, protect C.protect_fn) C.int {
	mutex.Lock()
	defer mutex.Unlock()
	if active != nil || protect == nil {
		return -1
	}
	duplicate, err := unix.Dup(int(fd))
	if err != nil {
		return -2
	}
	tunnel, _, err := tun.CreateUnmonitoredTUNFromFD(duplicate)
	if err != nil {
		unix.Close(duplicate)
		return -3
	}
	// Do not emit keys, peer addresses or traffic to Android logs.
	logger := &device.Logger{Verbosef: func(string, ...any) {}, Errorf: func(string, ...any) {}}
	d := device.NewDevice(tunnel, &protectedBind{Bind: conn.NewStdNetBind(), protect: protect}, logger)
	if err = d.IpcSet(C.GoString(settings)); err != nil {
		d.Close()
		return -4
	}
	d.DisableSomeRoamingForBrokenMobileSemantics()
	if err = d.Up(); err != nil {
		d.Close()
		return -5
	}
	active = d
	return 0
}

//export be_awg_handshake
func be_awg_handshake() C.longlong {
	mutex.Lock()
	defer mutex.Unlock()
	if active == nil {
		return -1
	}
	configuration, err := active.IpcGet()
	if err != nil {
		return -1
	}
	for _, line := range strings.Split(configuration, "\n") {
		if value, ok := strings.CutPrefix(line, "last_handshake_time_sec="); ok {
			seconds, _ := strconv.ParseInt(value, 10, 64)
			return C.longlong(seconds)
		}
	}
	return 0
}

//export be_awg_stop
func be_awg_stop() {
	mutex.Lock()
	defer mutex.Unlock()
	if active != nil {
		active.Close()
		active = nil
	}
}

func main() {}
