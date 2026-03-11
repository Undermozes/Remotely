using Microsoft.Extensions.Logging;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Remotely.Server.Services;

public interface IBackupService
{
    BackupData CreateBackup(RemotelyUser user, Device[] devices, DeviceGroup[] deviceGroups);
    string SerializeBackup(BackupData backup);
    BackupData? DeserializeBackup(string json);
    Task RestoreBackupAsync(BackupData backup, Device[] existingDevices, IDataService dataService);
}

public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger;
    }

    public BackupData CreateBackup(RemotelyUser user, Device[] devices, DeviceGroup[] deviceGroups)
    {
        var backup = new BackupData
        {
            BackupVersion = "1.0",
            CreatedAt = DateTimeOffset.Now,
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.UserOptions?.DisplayName,
            UserOptions = user.UserOptions,
            Devices = devices.Select(d => new DeviceBackupDto
            {
                ID = d.ID,
                DeviceName = d.DeviceName,
                Alias = d.Alias,
                Tags = d.Tags,
                Notes = d.Notes,
                DeviceGroupID = d.DeviceGroupID,
                Platform = d.Platform,
                OSDescription = d.OSDescription,
                CurrentUser = d.CurrentUser,
                Is64Bit = d.Is64Bit,
                ProcessorCount = d.ProcessorCount,
                TotalMemory = d.TotalMemory,
                TotalStorage = d.TotalStorage,
                PublicIP = d.PublicIP,
                AgentVersion = d.AgentVersion,
                LastOnline = d.LastOnline
            }).ToList(),
            DeviceGroups = deviceGroups.Select(g => new DeviceGroupBackupDto
            {
                ID = g.ID,
                Name = g.Name,
            }).ToList()
        };

        _logger.LogInformation(
            "Created backup for user {UserName} with {DeviceCount} devices and {GroupCount} device groups.",
            user.UserName,
            devices.Length,
            deviceGroups.Length);

        return backup;
    }

    public string SerializeBackup(BackupData backup)
    {
        return JsonSerializer.Serialize(backup, _jsonOptions);
    }

    public BackupData? DeserializeBackup(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupData>(json, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize backup data.");
            return null;
        }
    }

    public async Task RestoreBackupAsync(BackupData backup, Device[] existingDevices, IDataService dataService)
    {
        var existingDeviceIds = new HashSet<string>(
            existingDevices.Select(d => d.ID),
            StringComparer.OrdinalIgnoreCase);

        var restoredCount = 0;

        foreach (var deviceDto in backup.Devices)
        {
            if (string.IsNullOrEmpty(deviceDto.ID))
            {
                continue;
            }

            if (!existingDeviceIds.Contains(deviceDto.ID))
            {
                _logger.LogInformation(
                    "Skipping device {DeviceId} during restore because it no longer exists in the organization.",
                    deviceDto.ID);
                continue;
            }

            await dataService.UpdateDevice(
                deviceDto.ID,
                deviceDto.Tags,
                deviceDto.Alias,
                deviceDto.DeviceGroupID,
                deviceDto.Notes);

            restoredCount++;
        }

        _logger.LogInformation(
            "Restored {RestoredCount} devices from backup for user {UserName}.",
            restoredCount,
            backup.UserName);
    }
}
