using System;

namespace Hive.Framework.Shared
{
    [Flags]
    public enum PacketFlags : uint
    {
        None = 0,

        /// <summary>
        /// 指示封包在分发时应广播给所有会话
        /// </summary>
        Broadcast = 1 << 0,

        /// <summary>
        /// 指示封包已经经过最终处理，这种类型的数据包将不再被允许被重新转发、拆分和重打包
        /// </summary>
        Finalized = 1 << 1,

        /// <summary>
        /// 表示传输方要求终止连接
        /// </summary>
        RequestedShutdown = 1 << 2,

        RESERVED_2 = 1 << 3,
        RESERVED_3 = 1 << 4,
        RESERVED_4 = 1 << 5,
        RESERVED_5 = 1 << 6,
        RESERVED_6 = 1 << 7,
        RESERVED_7 = 1 << 8,
        RESERVED_8 = 1 << 9,
        RESERVED_9 = 1 << 10,

        /// <summary>
        /// 指示封包的发送方向为 C（Client） -> S（Server），该封包应严格遵循发送方向
        /// </summary>
        C2SPacket = 1 << 11,
        /// <summary>
        /// 指示封包的发送方向为 S（Server） -> C（Client），该封包应严格遵循发送方向
        /// </summary>
        S2CPacket = 1 << 12,

        RESERVED_12 = 1 << 13,
        RESERVED_13 = 1 << 14,
        RESERVED_14 = 1 << 15,
        RESERVED_15 = 1 << 16,
        RESERVED_16 = 1 << 17,
        RESERVED_17 = 1 << 18,
        RESERVED_18 = 1 << 19,
        RESERVED_19 = 1 << 20,

        /// <summary>
        /// 指示该数据包使用自定义的封包前缀
        /// </summary>
        HasCustomPacketPrefix = 1 << 21,

        /// <summary>
        /// 指示该数据包不包含有效载荷，由于封包不包含有效载荷，因此封包头部的 PacketID 也将被删除
        /// </summary>
        NoPayload = 1 << 22,

        RESERVED_22 = 1 << 23,
        RESERVED_23 = 1 << 24,
        RESERVED_24 = 1 << 25,
        RESERVED_25 = 1 << 26,
        RESERVED_26 = 1 << 27,
        RESERVED_27 = 1 << 28,
        RESERVED_28 = 1 << 29,
        RESERVED_29 = 1 << 30
    }
}