﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace Hive.Common.Shared.Helpers
{
    public static class NetworkHelper
    {
        private static readonly Random Random = new((int)DateTime.Now.Ticks);

        /// <summary>
        ///     获取操作系统已用的端口号
        /// </summary>
        /// <returns></returns>
        public static HashSet<int> PortIsUsed()
        {
            //获取本地计算机的网络连接和通信统计数据的信息            
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

            //返回本地计算机上的所有Tcp监听程序            
            var ipsTCP = ipGlobalProperties.GetActiveTcpListeners();

            //返回本地计算机上的所有UDP监听程序            
            var ipsUDP = ipGlobalProperties.GetActiveUdpListeners();

            //返回本地计算机上的Internet协议版本4(IPV4 传输控制协议(TCP)连接的信息。            
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            var allPorts = ipsTCP.Select(ep => ep.Port).ToList();
            allPorts.AddRange(ipsUDP.Select(ep => ep.Port));
            allPorts.AddRange(tcpConnInfoArray.Select(conn => conn.LocalEndPoint.Port));

            return new HashSet<int>(allPorts);
        }

        public static int GetRandomPort()
        {
            var hasUsedPort = PortIsUsed();
            var port = 0;
            var isRandomOk = true;

            while (isRandomOk)
            {
                port = Random.Next(1024, 65535);
                isRandomOk = hasUsedPort.Contains(port);
            }

            return port;
        }
    }
}