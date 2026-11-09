# OpenRGB read-only reader

The reader connects only to allowlisted literal IPv4 loopback addresses (`127.0.0.1` by default, or `127.0.0.2`), port 6742 by default. It takes a fresh SDK snapshot on each call, uses a 2.8-second overall network cancellation deadline, and closes the connection. Run it on a background task. It throws on unavailable servers, timeout, invalid protocol data, or device-list change so the caller can explicitly fall back. DNS names, remote addresses, abbreviated IP forms and other loopback addresses are rejected before connection.

The alternate `127.0.0.2` address can reach a wildcard-bound OpenRGB service when a separate GUI instance specifically owns `127.0.0.1` at the same port. This is an explicit connection choice, not automatic service discovery. Both addresses remain local; Graphite does not modify either OpenRGB instance. An isolated wildcard-bound test server verifies the alternate address; tests do not contact the user's service.

The outbound packet whitelist is exactly **40** (protocol negotiation), **0** (controller count), and **1** (controller descriptions). No lighting, mode, profile, rescan, hardware discovery, remote settings or write command is sent. No SDK server is started and no device driver/library is installed.

Protocol 1 or newer is required. Graphite announces maximum supported version **3**, and reads controller descriptions using `min(serverVersion, 3)`. Thus even protocol-6 servers use their documented legacy index/count/description format; Graphite does not guess modern unique IDs. On unsolicited device-list-change notification 100, the entire operation fails rather than reusing stale indexes. The next scheduled call can rediscover safely.

Bounds: 1 MiB per packet, 64 controllers, 4096 bytes per string, 256 modes/zones, and 4096 LEDs/colors. All variable records, terminators, block sizes, trailing bytes and matrix lengths/dimensions are checked. RGB byte order is decoded into an opaque WPF color.

Identity is SHA-256 over length-prefixed controller type, name, vendor, serial and location. It deliberately excludes enumeration index and colors, so reordering preserves identity. Callers must reject ambiguous duplicate identities and avoid assuming an empty/missing serial or location alone identifies a physical board. A changed location can require reselection. GPU type is **2** in the public enum.

Colors are OpenRGB's reported controller-state values. They are **not a claim of instantaneous physical LED readback**. Some hardware effects retain a static SDK color array while the actual LEDs animate. Following a single reported color is intentional; hardware effect behavior needs a real 5070 Ti observation.

## Automated fixtures

`GraphiteMonitor.Regression.OpenRgbChecks.Run()` hosts only isolated ephemeral loopback TCP servers. It checks protocol-6-to-3 negotiation, protocol 1/2 layouts, fragmented reads, mode colors, a real matrix-zone record, RGB order, controller reordering, a closed outbound whitelist, bad magic/IDs/lengths, excessive counts, stale-list notification, malformed matrix/string/block data, unexpected trailing data, disconnect, invalid port, and a stalled fragmented response deadline. These are synthetic protocol checks, not physical lighting validation. The shared regression entry point must invoke `Run()`.

Build and execution evidence is recorded by the coordinator after serial integration. No physical devices were accessed by this specialist.

## Primary protocol references

- [Official SDK structure tables and negotiation](https://github.com/CalcProgrammer1/OpenRGB/blob/master/Documentation/OpenRGBSDK.md)
- [Official controller serializer](https://github.com/CalcProgrammer1/OpenRGB/blob/master/RGBController/RGBController.cpp)
- [Official network server size fields and legacy response handling](https://github.com/CalcProgrammer1/OpenRGB/blob/master/NetworkServer.cpp)

No new package dependencies are required.
