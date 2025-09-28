using System;
using OwnerKeeper.Domain;

namespace OwnerKeeper.Tests.Domain;

/// <summary>
/// Phase 1: value objects and basic invariants.
/// Covers ResourceId, CameraState, ErrorCode, OperationTicket, CameraConfiguration.
/// (SCHEDULE Phase 1; SPECS §4.2, §6.1, §7; REQ-ST-001, REQ-ER-002, REQ-CF-001)
/// </summary>
[TestClass]
public class DomainBasicsTests
{
    [TestMethod]
    public void ResourceId_ToString_Includes_Kind_And_Value()
    {
        var id = new ResourceId(1, ResourceKind.Camera);
        Assert.AreEqual("Camera:1", id.ToString());
    }

    [TestMethod]
    public void CameraState_Contains_Required_States()
    {
        // (REQ-ST-001) Uninitialized .. Error must be present
        Assert.IsTrue(Enum.IsDefined(typeof(CameraState), CameraState.Uninitialized));
        Assert.IsTrue(Enum.IsDefined(typeof(CameraState), CameraState.Ready));
        Assert.IsTrue(Enum.IsDefined(typeof(CameraState), CameraState.Streaming));
        Assert.IsTrue(Enum.IsDefined(typeof(CameraState), CameraState.Error));
    }

    [TestMethod]
    public void ErrorCode_ToString_Is_Compact()
    {
        var code = new ErrorCode("OWN", 2001);
        Assert.AreEqual("OWN2001", code.ToString());
    }

    [TestMethod]
    public void OperationTicket_Accepted_Has_No_ErrorCode()
    {
        var t = OperationTicket.Accepted(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            nowUtc: DateTime.UnixEpoch
        );
        Assert.AreEqual(OperationTicketStatus.Accepted, t.Status);
        Assert.IsNull(t.ErrorCode);
    }

    [TestMethod]
    public void OperationTicket_FailedImmediately_Must_Contain_ErrorCode()
    {
        var t = OperationTicket.FailedImmediately(
            new ErrorCode("OWN", 2001),
            id: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            nowUtc: DateTime.UnixEpoch
        );
        Assert.AreEqual(OperationTicketStatus.FailedImmediately, t.Status);
        Assert.AreEqual("OWN2001", t.ErrorCode?.ToString());
    }

    [TestMethod]
    public void CameraConfiguration_Validate_Succeeds_For_Valid_Inputs()
    {
        var cfg = new CameraConfiguration(
            new CameraResolution(1920, 1080),
            PixelFormat.Rgb24,
            new FrameRate(30)
        );
        Assert.AreEqual(1920, cfg.Resolution.Width);
        Assert.AreEqual(30, cfg.FrameRate.Fps);
    }

    [TestMethod]
    public void CameraConfiguration_Validate_Throws_On_Invalid()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () =>
                new CameraConfiguration(
                    new CameraResolution(0, 1080),
                    PixelFormat.Rgb24,
                    new FrameRate(30)
                )
        );

        Assert.ThrowsExactly<ArgumentException>(
            () =>
                new CameraConfiguration(
                    new CameraResolution(1920, 1080),
                    PixelFormat.Rgb24,
                    new FrameRate(0)
                )
        );
    }
}
