using CoreApi.Entities;
using CoreApi.Models.Responses;

namespace CoreApi.Mappings;

public static class UserTabMappings
{
    public static UserTabResponse ToResponse(this UserTab tab) => new(tab.ProjectId, tab.LastPath);
}
