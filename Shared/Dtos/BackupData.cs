using Remotely.Shared.Models;
using System;
using System.Collections.Generic;

namespace Remotely.Shared.Dtos;

public class BackupData
{
    public string BackupVersion { get; set; } = "1.0";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public RemotelyUserOptions? UserOptions { get; set; }
    public List<DeviceBackupDto> Devices { get; set; } = new();
    public List<DeviceGroupBackupDto> DeviceGroups { get; set; } = new();
}

public class DeviceBackupDto
{
    public string? ID { get; set; }
    public string? DeviceName { get; set; }
    public string? Alias { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
    public string? DeviceGroupID { get; set; }
    public string? Platform { get; set; }
    public string? OSDescription { get; set; }
    public string? CurrentUser { get; set; }
    public bool Is64Bit { get; set; }
    public int ProcessorCount { get; set; }
    public double TotalMemory { get; set; }
    public double TotalStorage { get; set; }
    public string? PublicIP { get; set; }
    public string? AgentVersion { get; set; }
    public DateTimeOffset LastOnline { get; set; }
}

public class DeviceGroupBackupDto
{
    public string? ID { get; set; }
    public string? Name { get; set; }
}
