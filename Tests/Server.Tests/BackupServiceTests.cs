using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class BackupServiceTests
{
    private IDataService _dataService = null!;
    private IBackupService _backupService = null!;
    private TestData _testData = null!;

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
        _backupService = new BackupService(
            IoCActivator.ServiceProvider.GetRequiredService<ILogger<BackupService>>());
    }

    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestMethod]
    public void CreateBackup_ContainsUserInfo()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        var backup = _backupService.CreateBackup(user, devices, deviceGroups);

        Assert.IsNotNull(backup);
        Assert.AreEqual("1.0", backup.BackupVersion);
        Assert.AreEqual(user.UserName, backup.UserName);
        Assert.AreEqual(user.Email, backup.Email);
        Assert.IsTrue(backup.CreatedAt <= DateTimeOffset.Now);
    }

    [TestMethod]
    public void CreateBackup_ContainsDevices()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        var backup = _backupService.CreateBackup(user, devices, deviceGroups);

        Assert.AreEqual(2, backup.Devices.Count);
        Assert.IsTrue(backup.Devices.Any(d => d.ID == _testData.Org1Device1.ID));
        Assert.IsTrue(backup.Devices.Any(d => d.ID == _testData.Org1Device2.ID));
    }

    [TestMethod]
    public void CreateBackup_ContainsDeviceGroups()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        var backup = _backupService.CreateBackup(user, devices, deviceGroups);

        Assert.AreEqual(2, backup.DeviceGroups.Count);
        Assert.IsTrue(backup.DeviceGroups.Any(g => g.Name == "Org1Group1"));
        Assert.IsTrue(backup.DeviceGroups.Any(g => g.Name == "Org1Group2"));
    }

    [TestMethod]
    public void CreateBackup_DoesNotIncludeOtherOrgDevices()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        var backup = _backupService.CreateBackup(user, devices, deviceGroups);

        Assert.IsFalse(backup.Devices.Any(d => d.ID == _testData.Org2Device1.ID));
        Assert.IsFalse(backup.Devices.Any(d => d.ID == _testData.Org2Device2.ID));
    }

    [TestMethod]
    public void SerializeAndDeserialize_RoundTrip()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        var backup = _backupService.CreateBackup(user, devices, deviceGroups);
        var json = _backupService.SerializeBackup(backup);
        var restored = _backupService.DeserializeBackup(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(backup.BackupVersion, restored.BackupVersion);
        Assert.AreEqual(backup.UserName, restored.UserName);
        Assert.AreEqual(backup.Devices.Count, restored.Devices.Count);
        Assert.AreEqual(backup.DeviceGroups.Count, restored.DeviceGroups.Count);
    }

    [TestMethod]
    public void DeserializeBackup_InvalidJson_ReturnsNull()
    {
        var result = _backupService.DeserializeBackup("not valid json");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void DeserializeBackup_EmptyString_ReturnsNull()
    {
        var result = _backupService.DeserializeBackup("");
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task RestoreBackup_UpdatesDeviceMetadata()
    {
        var user = _testData.Org1Admin1;
        var devices = _dataService.GetDevicesForUser(user.UserName!);
        var deviceGroups = _dataService.GetDeviceGroups(user.UserName!);

        // Create backup
        var backup = _backupService.CreateBackup(user, devices, deviceGroups);

        // Modify backup data
        var deviceBackup = backup.Devices.First(d => d.ID == _testData.Org1Device1.ID);
        deviceBackup.Alias = "TestAlias";
        deviceBackup.Tags = "test-tag";
        deviceBackup.Notes = "Test notes";

        // Restore
        var existingDevices = _dataService.GetDevicesForUser(user.UserName!);
        await _backupService.RestoreBackupAsync(backup, existingDevices, _dataService);

        // Verify device was updated
        var updatedDevice = (await _dataService.GetDevice(_testData.Org1Device1.ID)).Value;
        Assert.IsNotNull(updatedDevice);
        Assert.AreEqual("TestAlias", updatedDevice.Alias);
        Assert.AreEqual("test-tag", updatedDevice.Tags);
        Assert.AreEqual("Test notes", updatedDevice.Notes);
    }

    [TestMethod]
    public async Task RestoreBackup_SkipsNonExistentDevices()
    {
        var user = _testData.Org1Admin1;

        var backup = new BackupData
        {
            UserName = user.UserName,
            Devices = new()
            {
                new DeviceBackupDto
                {
                    ID = "NonExistentDevice",
                    Alias = "Should not fail"
                }
            }
        };

        var existingDevices = _dataService.GetDevicesForUser(user.UserName!);

        // Should not throw
        await _backupService.RestoreBackupAsync(backup, existingDevices, _dataService);
    }
}
