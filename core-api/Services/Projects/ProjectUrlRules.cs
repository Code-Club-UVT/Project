using System.Net;
using System.Net.Sockets;
using CoreApi.Common.Results;

namespace CoreApi.Services.Projects;

/// <summary>
/// Checks the URLs an admin enters for a project. The gateway sends requests to the base URL, so without these checks an admin
/// could aim it at the host itself or at a cloud metadata service (SSRF). Private ranges stay allowed because containers
/// reach each other on them. Host names are not resolved here, so this is a first line of defence, not a complete one.
/// </summary>
public static class ProjectUrlRules
{
    public static Error? Validate(string url, string label, bool allowLocalAddresses)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return ProjectErrors.InvalidUrl($"{label} must be an absolute http or https URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return ProjectErrors.InvalidUrl($"{label} must not contain credentials, a query string or a fragment.");
        }

        if (!allowLocalAddresses && IsLocalHost(uri))
        {
            return ProjectErrors.InvalidUrl($"{label} must not point to a loopback, link-local or metadata address.");
        }

        return null;
    }

    private static bool IsLocalHost(Uri uri)
    {
        var host = uri.DnsSafeHost;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6
            ? address.IsIPv6LinkLocal
            : address.GetAddressBytes() is [169, 254, ..];
    }
}
