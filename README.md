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
* [Introduction](#introduction)
* [Motivation](#motivation)
* [Components](#components)
  * [Packet](#packet)
  * [ReadOnlyPacket](#readonlypacket)
  * [Channel](#channel)

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
This section documents in detail the usage of all the library components - if you ever get stuck, this is the place to look for answers.
* [Packet](#packet) allows you to easily create outgoing messages containing complex data, which is then sent over the network.
* [ReadOnlyPacket](#readonlypacket) allows you to easily read data from the received packet.
* [Channel](#channel) controls the way packets are sent and received. Also keeps track of network statistics.

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
In this phase, we are writing data to the previously created `Packet` instance. Link supports many different types out of the box:
1. [Primitive types: `string`, `byte`, `sbyte`, `bool`, `short`, `ushort`, `char`, `int`, `uint`, `float`, `long`, `ulong`, `double`.](https://github.com/FICHEKK/Link/blob/main/Examples/002-Complex-Packet/ComplexPacket.cs)
2. [Any `enum` of any underlying integral numeric type.](https://github.com/FICHEKK/Link/blob/main/Examples/004-Enums-In-Packets/EnumsInPackets.cs)
3. [Arrays, jagged arrays and array segments of any of the types above. For example, `string[]`, `int[][]`, `ArraySegment<double>` and so on.](https://github.com/FICHEKK/Link/blob/main/Examples/003-Arrays-In-Packets/ArraysInPackets.cs)

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
server.SendToOne(packet, clientEndPoints);
server.SendToAll(packet);
```

If some condition is preventing a packet from being sent, `Return` should be called on the packet instead. This will recycle internal buffer used by the `Packet`, allowing it to be reused later. If you forget to do so, nothing bad is going to happen, however new memory will need to be allocated, which will eventually trigger garbage collection. To see how many memory allocations were made in total, you can read static `Packet.AllocationCount` property - this value should eventually stagnate if you are properly recycling buffers.

```cs
packet.Return();
```

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
|  Sequenced |     ✔️    |        ✔️      |        -        |         -         |
|  Reliable  |     ✔️    |        ✔️      |        ✔️      |         ✔️        |

Each channel also has a name associated with it and keeps track of bandwidth statistics, which is useful for diagnosing network usage. Another powerful feature of Link is the [ability to easily define your own custom channels](https://github.com/FICHEKK/Link/blob/main/Examples/007-Custom-Channels/CustomChannels.cs). This way you can easily split your streams of data and keep track of how much bandwidth each stream consumes.
