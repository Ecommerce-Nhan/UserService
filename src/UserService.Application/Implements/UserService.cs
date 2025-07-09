using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Constants.Role;
using SharedLibrary.Dtos.Users;
using SharedLibrary.Filters;
using SharedLibrary.Requests.Identity;
using SharedLibrary.Response.Identity;
using SharedLibrary.Wrappers;
using UserService.Application.Interfaces;
using UserService.Domains.Entities;

namespace UserService.Application.Implements;

public class UserService(UserManager<User> userManager,
    RoleManager<Role> roleManager,
    IMapper mapper) : IUserService
{
    public async Task<PagedResponse<List<UserDto>>> GetAll(PaginationFilter pagination)
    {
        var pagedData = await GetPagedDataAsync(pagination);
        var result = mapper.Map<PagedResponse<List<UserDto>>>(pagedData);

        return result;
    }
    public async Task<IResponse<UserDto>> GetUserByIdAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        var result = mapper.Map<UserDto>(user);

        return await Response<UserDto>.SuccessAsync(result);
    }
    public async Task<IResponse> CreateUserAsync(RegisterRequest request)
    {
        var userWithSameUserName = await userManager.FindByNameAsync(request.UserName);
        if (userWithSameUserName != null)
        {
            return await Response.FailAsync(string.Format("Username {0} is already taken.", request.UserName));
        }
        var user = new User
        {
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserName = request.UserName,
            PhoneNumber = request.PhoneNumber,
            IsActive = request.ActivateUser,
            EmailConfirmed = request.AutoConfirmEmail,
            Address = request.Address,
            DateOfBirth = request.DateOfBirth?.ToUniversalTime()
        };

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var userWithSamePhoneNumber = await userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);
            if (userWithSamePhoneNumber != null)
            {
                return await Response.FailAsync(string.Format("Phone number {0} is already registered.", request.PhoneNumber));
            }
        }

        var userWithSameEmail = await userManager.FindByEmailAsync(request.Email);
        if (userWithSameEmail == null)
        {
            var result = await userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, RoleConstants.BasicRole);
                //if (!request.AutoConfirmEmail)
                //{
                //    var verificationUri = await SendVerificationEmail(user, origin);
                //    var mailRequest = new MailRequest
                //    {
                //        From = "mail@codewithmukesh.com",
                //        To = user.Email,
                //        Body = string.Format("Please confirm your account by <a href='{0}'>clicking here</a>.", verificationUri),
                //        Subject = "Confirm Registration"
                //    };
                //    BackgroundJob.Enqueue(() => _mailService.SendAsync(mailRequest));
                //    return await Response<string>.SuccessAsync(user.Id, string.Format(_localizer["User {0} Registered. Please check your Mailbox to verify!"], user.UserName));
                //}
                return await Response<string>.SuccessAsync(user.Id, string.Format("User {0} Registered.", user.UserName));
            }
            else
            {
                return await Response.FailAsync(result.Errors.Select(a => a.Description.ToString()).ToList());
            }
        }
        else
        {
            return await Response.FailAsync(string.Format("Email {0} is already registered.", request.Email));
        }
    }
    public async Task<bool> UpdateUserAsync(string id, UpdateUserDto input)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return false;
        }
        user.UserName = input.UserName;
        user.Email = input.Email;
        user.PhoneNumber = input.PhoneNumber;
        user.FirstName = input.FirstName;
        user.LastName = input.LastName;
        user.Address = input.Address;
        user.DateOfBirth = input.DateOfBirth;

        var identityResult = await userManager.UpdateAsync(user);
        return identityResult.Succeeded;
    }
    public async Task<bool> DeleteUserAsync(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return false;
        }

        var identityResult = await userManager.DeleteAsync(user);
        return identityResult.Succeeded;
    }

    private async Task<PagedResponse<List<User>>> GetPagedDataAsync(PaginationFilter pageFilter)
    {
        var validFilter = new PaginationFilter(pageFilter.PageNumber, pageFilter.PageSize);
        var query = userManager.Users.AsNoTracking();

        var pagedData = await query.Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                                   .Take(validFilter.PageSize)
                                   .ToListAsync();
        var totalRecords = await query.CountAsync();
        var response = new PagedResponse<List<User>>(pagedData, validFilter.PageNumber, validFilter.PageSize);

        var totalPages = ((double)totalRecords / validFilter.PageSize);
        response.TotalPages = Convert.ToInt32(Math.Ceiling(totalPages));
        response.TotalRecords = totalRecords;
        return response;
    }

    public async Task<IResponse<UserRoleResponse>> GetRolesAsync(string userId)
    {
        var viewModel = new List<UserRoleModel>();
        var user = await userManager.FindByIdAsync(userId);
        var roles = await roleManager.Roles.ToListAsync();
        if (user == null || roles == null)
        {
            return await Response<UserRoleResponse>.FailAsync($"Not Found User {userId} or don't have any role");
        }
        foreach (var role in roles)
        {
            var userRolesViewModel = new UserRoleModel
            {
                RoleName = role.Name!,
                RoleDescription = role.Description ?? string.Empty,
            };
            if (await userManager.IsInRoleAsync(user, role.Name!))
            {
                userRolesViewModel.Selected = true;
            }
            else
            {
                userRolesViewModel.Selected = false;
            }
            viewModel.Add(userRolesViewModel);
        }
        var result = new UserRoleResponse { UserRoles = viewModel };
        return await Response<UserRoleResponse>.SuccessAsync(result);
    }

    public async Task<IResponse> UpdateRolesAsync(UpdateUserRoleRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user == null)
        {
            return await Response.FailAsync($"Not Found User {request.UserId}.");
        }
        if (user.Email == "superadmin@gmail.com")
        {
            return await Response.FailAsync("Not Allowed.");
        }

        var roles = await userManager.GetRolesAsync(user);
        var selectedRoles = request.UserRoles.Where(x => x.Selected).ToList();

        //var currentUser = await userManager.FindByIdAsync(_currentUserService.UserId);
        //if (!await userManager.IsInRoleAsync(currentUser, RoleConstants.AdministratorRole))
        //{
        //    var tryToAddAdministratorRole = selectedRoles
        //        .Any(x => x.RoleName == RoleConstants.AdministratorRole);
        //    var userHasAdministratorRole = roles.Any(x => x == RoleConstants.AdministratorRole);
        //    if (tryToAddAdministratorRole && !userHasAdministratorRole || !tryToAddAdministratorRole && userHasAdministratorRole)
        //    {
        //        return await Response.FailAsync("Not Allowed to add or delete Administrator Role if you have not this role.");
        //    }
        //}

        var result = await userManager.RemoveFromRolesAsync(user, roles);
        result = await userManager.AddToRolesAsync(user, selectedRoles.Select(y => y.RoleName));
        return await Response.SuccessAsync("Roles Updated");
    }
}