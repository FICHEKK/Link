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

## Introduction
Link is a networking library that fills the gap between UDP and TCP, allowing you to easily create complex, high-performance, low-latency applications.
It uses exclusively UDP, but implements important features found in TCP and allows the user to choose which features to use. Link solves the following problems:
* **Client and server connection**: Create server and connect with client(s) in just a few clean lines of code.
* **Packet creation and handling**: Easily write complex data to packets that can be just as easily read and handled on the receiving side.
* **Packet delivery methods**: Choose how your packet should be delivered over the network - allows you to enable duplication-prevention, ordering, reliability and fragmentation.

Link is written in pure `C#` and targets `.NET Standard 2.0`.

## Motivation
There are plenty of existing RUDP (reliable UDP) library implementations. However, most are not user-friendly, have too many unnecessary features and a steep learning curve which can be frustrating for new users. To make it worse, most are hardly even documented, making their use even harder. Link learned from their mistakes (and their strengths as well) and delivers an extremely pleasant user experience, without sacrificing any performance. To demonstrate simplicity and expressiveness of Link, check out the ["Hello world"](https://github.com/FICHEKK/Link/blob/main/Examples/001-Hello-World/HelloWorld.cs) example:

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
