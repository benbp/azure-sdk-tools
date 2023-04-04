using Azure;
using Azure.Core;
using Azure.Identity;
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

    public async Task CreateRoleAssignment(ServicePrincipal servicePrincipal, RoleBasedAccessControl rbac)
    {
        var resource = ArmClient.GetGenericResource(new ResourceIdentifier(rbac.Scope!));
        var roles = resource.GetAuthorizationRoleDefinitions().GetAllAsync($"roleName eq '{rbac.Role}'");
        var allroles = new List<AuthorizationRoleDefinitionResource>();
        await foreach (var r in roles)
        {
            allroles.Add(r);
        }

        var assignment = RoleAssignmentResource.CreateResourceIdentifier(rbac.Scope, rbac.Role);
        var role = await resource.GetAuthorizationRoleDefinitions().GetAllAsync($"roleName eq '{rbac.Role}'").FirstAsync();
        Console.WriteLine($"Found role '{role.Data.RoleName}' with id '{role.Data.Name}'");

        var principalId = Guid.Parse(servicePrincipal?.Id ?? string.Empty);
        var content = new RoleAssignmentCreateOrUpdateContent(role.Data.Id, principalId);
        content.PrincipalType = RoleManagementPrincipalType.ServicePrincipal;

        Console.WriteLine($"Creating role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'...");
        var roleAssignments = resource.GetRoleAssignments();
        var allAssignments = new List<RoleAssignmentResource>();
        await foreach (var a in roleAssignments)
        {
            allAssignments.Add(a);
        }
        var spAssignment = allAssignments.Where(a => a.Data.PrincipalId == principalId).FirstOrDefault();
        // var assignmentName = "/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/rg-bebroder-acess-test/providers/Microsoft.KeyVault/vaults/bebroderaccesstest/providers/Microsoft.Authorization/roleAssignments/7e545899-dd1e-43e1-8ff3-a62b41497f74";
        // await resource.GetRoleAssignments().CreateOrUpdateAsync(WaitUntil.Completed, assignmentName, content);
        await resource.GetRoleAssignments().CreateOrUpdateAsync(WaitUntil.Completed, role.Data.Name, content);
        Console.WriteLine($"Created role assignment for principal '{principalId}' with role '{role.Data.RoleName}' in scope '{rbac.Scope}'");
    }
}

public interface IRbacClient
{
    public Task CreateRoleAssignment(ServicePrincipal app, RoleBasedAccessControl rbac);
}