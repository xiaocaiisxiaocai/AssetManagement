using System.ComponentModel.DataAnnotations;
using AssetManagement.Application.TestMaterials;
using AssetManagement.Application.Workflow;
using FluentAssertions;

namespace AssetManagement.Tests.TestMaterials;

public class RequestLengthValidationTests
{
    [Fact]
    public void Test_project_and_followup_requests_match_database_lengths()
    {
        InvalidMember(new SaveTestProjectRequest { Name = new string('项', 101) }, nameof(SaveTestProjectRequest.Name));
        InvalidMember(new SaveTestProjectRequest { Code = new string('编', 51) }, nameof(SaveTestProjectRequest.Code));
        InvalidMember(new SaveTestProjectRequest { ProjectTypeCode = new string('类', 51) }, nameof(SaveTestProjectRequest.ProjectTypeCode));
        InvalidMember(new SaveTestProjectRequest { ProgressCode = new string('进', 51) }, nameof(SaveTestProjectRequest.ProgressCode));
        InvalidMember(new SaveTestProjectRequest { TestStatus = new string('状', 1001) }, nameof(SaveTestProjectRequest.TestStatus));
        InvalidMember(new SaveTestProjectFollowupRequest { Content = new string('跟', 2001) }, nameof(SaveTestProjectFollowupRequest.Content));
    }

    [Fact]
    public void Material_and_flow_requests_match_database_lengths()
    {
        InvalidMember(new SaveTestMaterialRequest { Name = new string('料', 101) }, nameof(SaveTestMaterialRequest.Name));
        InvalidMember(new SaveTestMaterialRequest { VendorName = new string('供', 101) }, nameof(SaveTestMaterialRequest.VendorName));
        InvalidMember(new SaveTestMaterialRequest { Model = new string('型', 101) }, nameof(SaveTestMaterialRequest.Model));
        InvalidMember(new SaveTestMaterialRequest { Brand = new string('牌', 101) }, nameof(SaveTestMaterialRequest.Brand));
        InvalidMember(new SaveTestMaterialRequest { Remark = new string('备', 501) }, nameof(SaveTestMaterialRequest.Remark));
        InvalidMember(new InitiateTransferRequest { Reason = new string('因', 501) }, nameof(InitiateTransferRequest.Reason));
        InvalidMember(new MaterialApprovalRequest { Opinion = new string('意', 501) }, nameof(MaterialApprovalRequest.Opinion));
        InvalidMember(new MaterialRejectRequest { Reason = new string('因', 501) }, nameof(MaterialRejectRequest.Reason));
        InvalidMember(new StartApprovalRequest { Reason = new string('因', 501) }, nameof(StartApprovalRequest.Reason));
        InvalidMember(new ApprovalActionRequest { Opinion = new string('意', 501) }, nameof(ApprovalActionRequest.Opinion));
        InvalidMember(new RejectRequest { Reason = new string('因', 501) }, nameof(RejectRequest.Reason));
    }

    private static void InvalidMember(object request, string member)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true)
            .Should().BeFalse();
        results.Should().Contain(result => result.MemberNames.Contains(member));
    }
}
