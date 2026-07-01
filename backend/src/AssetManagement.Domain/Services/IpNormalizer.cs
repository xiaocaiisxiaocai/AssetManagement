using System.Net;
using System.Net.Sockets;

namespace AssetManagement.Domain.Services;

/// <summary>
/// 将采集到的客户端 IP 归一化为可读的 IPv4 形式:
/// IPv6 回环 <c>::1</c> → <c>127.0.0.1</c>;IPv4-mapped IPv6(<c>::ffff:a.b.c.d</c>)→ <c>a.b.c.d</c>;
/// 已是 IPv4 原样返回;无法映射的真实 IPv6 保留原值。空值/非法值原样返回。
/// </summary>
public static class IpNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        var text = raw.Trim();
        // 去掉 IPv6 作用域标识(如 fe80::1%eth0)与可能存在的端口无关部分
        var zoneIndex = text.IndexOf('%');
        var candidate = zoneIndex >= 0 ? text[..zoneIndex] : text;

        if (!IPAddress.TryParse(candidate, out var ip))
        {
            return text;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return ip.ToString();
        }

        if (IPAddress.IsLoopback(ip))
        {
            return "127.0.0.1";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4().ToString();
        }

        // 真实的公网/局域网 IPv6,无法安全地转换为 IPv4,保留原值
        return ip.ToString();
    }
}
