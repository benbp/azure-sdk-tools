using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Administration;
using Azure.Security.KeyVault.Administration.Models;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Microsoft.Graph.Models;

public class RbacClient : IRbacClient
{
    public ArmClient ArmClient { get; set; }

    public RbacClient()
    {
        ArmClient = new ArmClient(new DefaultAzureCredential());
    }

    public async Task CreateRoleAssignmentCreateRoleAssignmentRequest(ServicePrincipal servicePrincipal, RoleBasedAccessControl rbac)
    {
        throw new Exception("FOO");
        var resource = ArmClient.GetGenericResource(new ResourceIdentifier(rbac.Scope!));
        var role = await resource.GetAuthorizationRoleDefinitions().GetAllAsync($"roleName eq '{rbac.Role}'").FirstAsync();
        Console.WriteLine($"Found role '{role.Data.RoleName}' with id '{role.Data.Name}'");

        // resource.UpdateAuthorizationRoleAssignments().CreateOrUpdate(role.Data.Name, new RoleAssignmentData(role.Data.Id, rbac.PrincipalId));

        var assignment = RoleAssignmentResource.CreateResourceIdentifier(rbac.Scope, rbac.Role);
        // var principalId = Guid.Parse(app?.AppId ?? string.Empty);
        var principalId = Guid.Parse(servicePrincipal?.Id ?? string.Empty);
        Console.WriteLine($"DEBUG principal '{principalId.ToString()}'");
        var content = new RoleAssignmentCreateOrUpdateContent(role.Data.Id, principalId);
        Console.WriteLine($"Creating role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'...");
        await resource.GetRoleAssignments().CreateOrUpdateAsync(WaitUntil.Completed, role.Data.Name, content);
        Console.WriteLine($"Created role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'");

        // await resource.UpdateAsync(WaitUntil.Completed, resource.Data);
    }
}

public interface IRbacClient
{
    public Task CreateRoleAssignmentCreateRoleAssignmentRequest(ServicePrincipal app, RoleBasedAccessControl rbac);
}