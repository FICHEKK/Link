<p align="center">
  <img src="https://github.com/FICHEKK/Link/blob/main/Docs/Logo.png?raw=true" />
</p>

<div align="center">
  <h3><i>"Link is a simple, easy-to-use, but powerful reliable UDP library."</i></h3>
</div>

<div align="center">
  
  [![GitHub license](https://badgen.net/github/license/Naereen/Strapdown.js)](https://github.com/FICHEKK/Link/blob/main/LICENSE)
  
</div>

## Table of contents
* [Introduction](#introduction) gives brief explanation of what Link is.
* [Motivation](#motivation) describes why Link was created.
* [Components](#components) documents in detail the usage of all the Link components.
  * [Packet](#packet) allows you to easily create outgoing messages containing complex data, which is then sent over the network.
    * [Initialization phase](#1-initialization-phase)
    * [Writing phase](#2-writing-phase)
    * [Sending phase](#3-sending-phase)
  * [ReadOnlyPacket](#readonlypacket) allows you to easily read data from the received packet.
  * [Channel](#channel) controls the way packets are sent and received. Also keeps track of network statistics.
  * [Client](#client) allows you to connect, communicate and disconnect from the server.
    * [Connecting to the server](#connecting-to-the-server)
    * [Sending packets to the server](#sending-packets-to-the-server)
    * [Disconnecting from the server](#disconnecting-from-the-server)
    * [Events](#events)
  * [Server](#server) allows you to start listening for, communicate with, and manage multiple client connections.
    * [Starting a server](#starting-a-server)
    * [Sending packets to clients](#sending-packets-to-clients)
    * [Kicking clients](#kicking-clients)
    * [Stopping a server](#stopping-a-server)
    * [Events](#events-1)
  * [Serializers](#serializers) allows you to easily extend serialization system to support custom data types.
    * [Built-in serializers](#built-in-serializers)
    * [Serializing types out of your control](#serializing-types-out-of-your-control)
    * [Serializing types under your control](#serializing-types-under-your-control)
    * [Using third-party libraries](#using-third-party-libraries)
  * [Log](#log) allows you to easily log information, warning or error messages however you like.

## Introduction
Link is a networking library that fills the gap between UDP and TCP, allowing you to easily create complex, high-performance, low-latency applications.
It uses exclusively UDP, but implements important features found in TCP and allows the user to choose which features to use. Link solves the following problems:
* **Client and server connection**: Create server and connect with client(s) in just a few clean lines of code.
* **Packet creation and handling**: Easily write complex data to packets that can be just as easily read and handled on the receiving side.
* **Packet delivery methods**: Choose how your packet should be delivered over the network - allows you to enable duplication-prevention, ordering, reliability and fragmentation.

Link is written in pure `C#` and targets `.NET Standard 2.0`.

## Motivation
There are plenty of existing reliable UDP library implementations. However, most are not user-friendly, have too many unnecessary features and a steep learning curve which can be frustrating for new users. To make it worse, most are hardly even documented, making their use even harder. Link learned from their mistakes (and their strengths as well) and delivers an extremely pleasant user experience, without sacrificing any performance. To demonstrate simplicity and expressiveness of Link, check out the ["Hello world"](https://github.com/FICHEKK/Link/blob/main/Examples/001-Hello-World/HelloWorld.cs) example:

```cs
1 using var server = new Server();
2 server.AddHandler(args => Console.WriteLine(args.Packet.Read<string>()));
3 server.Start(Port);
4 
5 using var client = new Client();
6 client.Connected += args => args.Client.Send(Packet.Get(Delivery.Reliable).Write("Hello world!"));
7 client.Connect(IpAddress, Port);
```

In less than 10 lines of code, Link manages to:
1. Create and start a new server on the specified port. (Lines `1` and `3`)
2. Create and connect a new client to the server. (Lines `5` and `7`)
3. Construct and send a packet in reliable manner when client connects. (Line `6`)
4. Listen and react to received packet on the server. (Line `2`)
5. Clean-up all the used network resources. (`using` on lines `1` and `5`)

And this is just the beginning! Link offers many more amazing features, which are just as simple and easy-to-use.

## Components

### [Packet](https://github.com/FICHEKK/Link/blob/main/Examples/002-Complex-Packet/ComplexPacket.cs)
`Packet` represents a single **outgoing** message of arbitrary data that can be sent over the network. A lifecycle of `Packet` instance consists of three phases:

#### 1. Initialization phase
In this phase, we are creating a new `Packet` instance with the following properties:
1. `delivery` specifies how a packet should be delivered to the remote destination.
2. `packetId` specifies identifier of the packet that is used by the receiver to handle different packet types.

**Note:** Always use static `Packet.Get` method for creating packets.
 
```cs
// Creates a packet that will be delivered reliably. Packet ID is set to default value of 65535.
var packet = Packet.Get(Delivery.Reliable);

// Creates a packet that will be sent unreliably and has ID of 7.
var packet = Packet.Get(Delivery.Unreliable, packetId: 7);
```

#### 2. Writing phase
In this phase, we are writing data to the previously created `Packet` instance. Link supports many different types out of the box, as explained in ["Serializers" section](#serializers).

```cs
// Writes a string to the packet.
packet.Write("Text");

// Writes an integer to the packet.
packet.Write(12345);

// Writes an array of doubles to the packet.
packet.Write(new[] { 4.0, 5.0, 6.0 });

// Write methods can also be chained.
packet.Write('A').Write('B').Write('C');
```

#### 3. Sending phase
In the last phase, packet should be sent by using any of the send methods defined in `Client` or `Server`.

```cs
client.Send(packet);
server.SendToOne(packet, clientEndPoint);
server.SendToMany(packet, clientEndPoints);
server.SendToAll(packet);
```

If some condition is preventing a packet from being sent, `Return` should be called on the packet instead. This will recycle internal buffer used by the `Packet`, allowing it to be reused later. If you forget to do so, nothing bad is going to happen, however new memory will need to be allocated, which will eventually trigger garbage collection. To see how many memory allocations were made in total, you can read static `Packet.AllocationCount` property - this value should eventually stagnate if you are properly recycling buffers.

```cs
packet.Return();
```

---

### [ReadOnlyPacket](https://github.com/FICHEKK/Link/blob/main/Examples/002-Complex-Packet/ComplexPacket.cs)
`ReadOnlyPacket` represents a single **incoming** message of arbitrary data that can be received over the network. It exposes a read-only view into received data and cannot in any way modify underlying data. If a packet was received that had data written as demonstrated in the [writing phase](#2-writing-phase), this is how it could be read:

**Note:** Order matters! Data must be read in the same order it was originally written to the packet.

```cs
// Read string first as it was written first.
var @string = packet.Read<string>();

// Next thing on the list is an integer.
var @int = packet.Read<int>();

// Then an array of doubles...
var array = packet.Read<double[]>();

// Finally, the 3 characters.
var char1 = packet.Read<char>();
var char2 = packet.Read<char>();
var char3 = packet.Read<char>();
```

---

### [Channel](https://github.com/FICHEKK/Link/blob/main/Examples/006-Default-Channels/DefaultChannels.cs)
Channel represents a component that controls the way packets are sent and received. This is the core of what makes Link a reliable UDP library. There are four fundamental problems that a channel can, but does not have to solve:
1. **Order** - ability for the receiver to receive packets in order in which they were originally sent.
2. **Duplication** - ability for receiver to detect and discard duplicate packets.
3. **Reliability** - ability for sender to ensure that receiver received a packet.
4. **Fragmentation** - ability for sender to divide a big packet into smaller packets called fragments and for receiver to reassemble those into original big packet.

Based on that, Link contains multiple channel implementations, each solving a subset of problems:

1. **Unreliable** solves none of the problems and acts as a pure UDP channel. This means packets can arrive out of order, be duplicated in the network, be lost and do not support fragmentation.
2. **Sequenced** solves two problems: order and duplication. Order is ensured by attaching a sequence number to each outgoing packet. If an older or duplicate packet arrives, it will be discarded.
3. **Reliable** solves all of the problems and acts as a TCP stream. It ensures packets arrive using acknowledgement and retransmission system.

|  **Name**  | **Order** | **Duplication** | **Reliability** | **Fragmentation** |
|:----------:|:---------:|:---------------:|:---------------:|:-----------------:|
| Unreliable |     -     |        -        |        -        |         -         |
|  Sequenced |     ??????    |        ??????      |        -        |         -         |
|  Reliable  |     ??????    |        ??????      |        ??????      |         ??????        |

Each channel also has a name associated with it and keeps track of bandwidth statistics, which is useful for diagnosing network usage. Another powerful feature of Link is the [ability to easily define your own custom channels](https://github.com/FICHEKK/Link/blob/main/Examples/007-Custom-Channels/CustomChannels.cs). This way you can easily split your streams of data and keep track of how much bandwidth each stream consumes.

---

### Client
Represents a network node that has one to one relationship with the server.

#### Connecting to the server
```cs
public void Connect(string ipAddress, ushort port, int maxAttempts = 5, int delayBetweenAttempts = 1000, ConnectPacketFactory? connectPacketFactory = null);
```

`ipAddress` - IPv4 address of the server to which client should attempt to connect, such as `127.0.0.1`.

`port` - Port on which server is listening, such as `7777`.

`maxAttempts` - Since library is sending UDP connect packets, they might be lost on the way. This parameter defines the maximum number of connect attempts before considering server as unreachable.

`delayBetweenAttempts` - Delay between consecutive connect attempts, in milliseconds.

`connectPacketFactory` -  [Allows additional data to be written to the connect packet.](https://github.com/FICHEKK/Link/blob/main/Examples/009-Connection-Validator/ConnectionValidator.cs)

```cs
var client = new Client();

// Basic usage.
client.Connect("127.0.0.1", 7777);

// Write additional data to the connect packet.
client.Connect("127.0.0.1", 7777, connectPacketFactory: packet => packet.Write(ServerKey));
```

#### Sending packets to the server
Sending packets to the server is performed by calling the `Send` method and providing `Packet` containing payload.

**Note:** You can only start sending packets once client successfully connects. To know when that happens, [subscibe to `Client.Connected` event](#events).

```cs
// One-liner that sends "Hello server!" in a reliable manner.
client.Send(Packet.Get(Delivery.Reliable).Write("Hello server!"));
```

#### Disconnecting from the server
To properly disconnect and dispose the underlying socket, call `Disconnect` method.

```cs
client.Disconnect();
```

#### Events
[`Client` exposes important events that can be easily subscribed to. Each event provides event arguments, containing useful information about the event.](https://github.com/FICHEKK/Link/blob/main/Examples/014-Network-Events/NetworkEvents.cs)

```cs
// Invoked each time client starts the process of establishing connection with the server.
client.Connecting += _ => Console.WriteLine("Client is connecting to the server.");

// Invoked each time client successfully connects to the server.
client.Connected += _ => Console.WriteLine("Client has connected to the server.");

// Invoked each time client fails to establish a connection with the server.
client.ConnectFailed += _ => Console.WriteLine("Client failed to connect to the server.");

// Invoked each time client disconnects from the server.
client.Disconnected += args => Console.WriteLine($"Client has disconnected from the server (cause: {args.Cause}).");
```

---

### Server
Represents a network node that has one to many relationship with clients.

#### Starting a server
```cs
public void Start(ushort port, int maxConnectionCount = -1);
```

`port` - Port on which server should listen on.

`maxConnectionCount` - Maximum allowed number of simultaneous client connections. If set to a negative value, there is no connection limit.

```cs
var server = new Server();

// Basic usage, no client connection limit.
server.Start(port: 7777);

// Start listening on port 7777, and allow up to 3 client connections.
server.Start(port: 7777, maxConnectionCount: 3);
```

#### Sending packets to clients
[Since `Server` can contain multiple client connections, it exposes multiple send methods:](https://github.com/FICHEKK/Link/blob/main/Examples/016-Server-Send-Methods/ServerSendMethods.cs)

```cs
public void SendToOne(Packet packet, EndPoint clientEndPoint);
public void SendToMany(Packet packet, IEnumerable<EndPoint> clientEndPoints);
public void SendToAll(Packet packet);
```

`SendToOne` sends a packet to one particular client. `SendToMany` sends a packet to many clients. `SendToAll` sends a packet to all clients.

#### Kicking clients
If a malicious or unruly client is detected, that client can be deliberately disconnected from the server by calling the `Kick` method.

```cs
server.Kick(clientEndPoint);
```

#### Stopping a server
To properly stop a server by disposing of the underlying socket and disconnecting all of the connected clients, call the `Stop` method.

```cs
server.Stop();
```

#### Events
[`Server` exposes important events that can be easily subscribed to. Each event provides event arguments, containing useful information about the event.](https://github.com/FICHEKK/Link/blob/main/Examples/014-Network-Events/NetworkEvents.cs)

```cs
// Invoked each time server starts and begins listening for client connections.
server.Started += args => Console.WriteLine($"Server started on port {args.Server.LocalEndPoint!.Port}.");

// Invoked each time a new client connects to the server.
server.ClientConnected += args => Console.WriteLine($"Client from {args.Connection.RemoteEndPoint} has connected.");

// Invoked each time an already connected client disconnects from the server.
server.ClientDisconnected += args => Console.WriteLine($"Client from {args.Connection.RemoteEndPoint} has disconnected (cause: {args.Cause}).");

// Invoked each time server stops and no longer listens for client connections.
server.Stopped += _ => Console.WriteLine("Server stopped.");
```

---

### Serializers
While Link is **not** a serialization library, it still attempts to make your experience much more pleasant by allowing you to easily serialize any data type. At the core of the serialization system is a simple generic `ISerializer` interface.

```cs
public interface ISerializer<T>
{
    public void Write(Packet packet, T value);
    public T Read(ReadOnlyPacket packet);
}

```

#### Built-in serializers
By default, Link provides `ISerializer` implementations for the following types:
1. [Primitive types: `string`, `byte`, `sbyte`, `bool`, `short`, `ushort`, `char`, `int`, `uint`, `float`, `long`, `ulong`, `double`.](https://github.com/FICHEKK/Link/blob/main/Examples/002-Complex-Packet/ComplexPacket.cs)
2. [Any `enum` of any underlying integral numeric type.](https://github.com/FICHEKK/Link/blob/main/Examples/004-Enums-In-Packets/EnumsInPackets.cs)
3. [Arrays, jagged arrays and array segments of any of the types above. For example, `string[]`, `int[][]`, `ArraySegment<double>` and so on.](https://github.com/FICHEKK/Link/blob/main/Examples/003-Arrays-In-Packets/ArraysInPackets.cs)

These types can be written to `Packet` and read from `ReadOnlyPacket` without any work required by the user.

---

#### Serializing types out of your control
Supporting serialization of types that you cannot modify (such as .NET types or third-party library types) is extremely simple: 

#### 1. Create a custom `ISerializer` implementation
```cs
public class DateTimeSerializer : ISerializer<DateTime>
{
    public void Write(Packet packet, DateTime dateTime) => packet.Write(dateTime.Ticks);

    public DateTime Read(ReadOnlyPacket packet) => new(ticks: packet.Read<long>());
}
```

#### 2. Register serializer implementation
```cs
Serializers.Add(new DateTimeSerializer());
```

And that's it! You can now read and write `DateTime` the same way you would any of the built-in types. But not only `DateTime`; arrays, jagged arrays and any serializable collections of `DateTime` will work also!

---

#### Serializing types under your control
Supporting serialization of types under your control (types that you can modify) can be done in two ways:
1. Custom `ISerializer` implementation - explained in section ["Serializing types out of your control"](#serializing-types-out-of-your-control).
2. Self-serializable implementation - explained in this section.

Self-serializable implementation is an implementation in which a type `T` implements `ISerializer<T>` interface, providing serialization methods for itself. Here is one such example:

```cs
public readonly struct Point : ISerializer<Point>
{
    public float X { get; }
    public float Y { get; }

    public Point(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Write(Packet packet, Point point)
    {
        packet.Write(point.X);
        packet.Write(point.Y);
    }

    public Point Read(ReadOnlyPacket packet)
    {
        var x = packet.Read<float>();
        var y = packet.Read<float>();
        return new Point(x, y);
    }
}
```

**Self-serializable implementations do not require explicit `Serializers.Add` method call.** Simply implementing this pattern will automatically allow serialization of the type, as long as the following condition is fulfilled: type must have public default parameterless constructor - needs to fulfill `new()` generic constraint. This is fulfilled by all `struct` types by default, but classes need to have it defined (implicitly or explicitly).

---

#### Using third-party libraries
If none of the solutions above are enough for your use-case, it is suggested that you utilize a well-tested third-party serialization library for converting your types to `byte[]` (binary format) or `string` (textual format), which can then be easily written to `Packet` and read from `ReadOnlyPacket`. Read and write operations of `byte[]` were especially optimized and are extremely fast - and so is `string` as it uses aforementioned `byte[]` operations.

```cs
// Fictional example to demonstrate how it could look like.
var bytes = ThirdPartyLibrary.Serialize(myComplexObject);

// Fill packet with serialized object bytes.
var packet = Packet.Get(Delivery.Reliable).Write(bytes);

...

// On the receiver side, read bytes.
var bytes = readOnlyPacket.Read<byte[]>();

// Reconstruct the original object.
var myComplexObject = ThirdPartyLibrary.Deserialize<MyComplexObject>(bytes);
```

---

### [Log](https://github.com/FICHEKK/Link/blob/main/Examples/011-Logger-Initialization/LoggerInitialization.cs)
Logging is extremely important in all types of applications, but especially in networked ones due to their intrinsic complexity. That is why Link defines a simple, but fully customizable logging component. By default, its implementation is empty and it is up to you to define where and how logs should be written. Fortunately, this process could not be simpler:

```cs
// Log useful information to the standard output stream.
Log.Info = Console.Out.WriteLine;

// Same with warnings...
Log.Warning = Console.Out.WriteLine;

// Errors get logged to the standard error output stream.
Log.Error = Console.Error.WriteLine;
```

You can go as complex as you want:

```cs
// Since loggers are simple delegates, we can execute any custom
// logging logic that we need. For example, we can easily write
// current time to each message, just like in this example. Or
// we could write logging information to an external destination,
// such as a text file or database.
Log.Info = message => Console.WriteLine($"[{DateTime.Now}] {message}");
```

Link uses these loggers internally and it is recommended that you do also:

```cs
// We also can (and should!) use loggers ourselves:
Log.Info("Useful information that helps during development.");
Log.Warning("Warning, something might be wrong!");
Log.Error("Something went definitely wrong!");
```
