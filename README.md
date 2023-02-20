# Hive.Framework [![.NET](https://github.com/Corona-Studio/Hive.Framework/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Corona-Studio/Hive.Framework/actions/workflows/dotnet.yml)

蜂巢，一个开源的游戏服务器框架，旨在提供高性能、高扩展性、高度自由的游戏后端服务器的开发流程。（包括一个官方示例实现）

## 项目简介

|  项目名 |    说明   |
|:------:|:--------:|
|Hive.Framework.Shared|共用库，用于存放通用的方法和对象实现|
|Hive.Framework.Codec.Abstractions|编解码器规范抽象，使用该库开发自定义的编解码器|
|Hive.Framework.Codec.Protobuf|基于 ProtoBuf 的官方解码器实现，提供开箱即用的体验|
|Hive.Framework.Networking.Abstractions|网络库规范抽象，使用该库开发自定义的网络实现|
|Hive.Framework.Networking.Shared|网络库共享项目，用于存放网络库的默认抽象和共用方法，使用该库开发自定义的网络实现|
|Hive.Framework.Networking.Tcp|基于 TCP 的官方网络库实现，提供开箱即用的体验|
|Hive.Framework.Networking.Udp|基于 UDP 的官方网络库实现，提供开箱即用的体验|
|Hive.Framework.Networking.Quic|基于 QUIC 的官方网络库实现，提供开箱即用的体验|
|Hive.Framework.Networking.Kcp|基于 KCP 的官方网络库实现，提供开箱即用的体验|
