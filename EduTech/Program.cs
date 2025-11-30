using EduTech;
using EduTech.DbInitializer;
using EduTech.Models;
using EduTech.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Licensing;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<EduTechDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IDbInitializer, DbInitializer>();

//Register Syncfusion license
SyncfusionLicenseProvider.RegisterLicense(
    "Mgo+DSMBPh8sVXJ8S0d+X1JPd11dXmJWd1p/THNYflR1fV9DaUwxOX1dQl9nSHxSckdjXXtbcnRSRWA=");


// Add mail services to the container.

builder.Services.AddOptions();
var mailsetting = builder.Configuration.GetSection("MailSettings");
builder.Services.Configure<MailSettings>(mailsetting);
builder.Services.AddSingleton<IEmailSender, SendMailService>();



//  Add Azure Email service configuration
// builder.Services.AddSingleton(_ =>
// {
//     var connectionString = builder.Configuration.GetConnectionString("EmailConnectionString");
//     if (string.IsNullOrEmpty(connectionString))
//         throw new InvalidOperationException("Email connection string is not configured");
//     return new EmailClient(connectionString);
// });

//builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
//builder.Services.AddTransient<PdfConverter>();


//builder.Services.AddScoped<IEmailSender, EmailSender>();

// Adds the Identity system, including the default UI, and configures the user type as IdentityUser
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true; // Require email confirmation
            options.Lockout.AllowedForNewUsers = true;
            options.Password.RequiredLength = 8; // Độ dài tối thiểu
            options.Password.RequireNonAlphanumeric = true; // Kí tự đặc biệt
            options.Password.RequireDigit = true; // Số
        }
    )
    .AddEntityFrameworkStores<EduTechDbContext>(); // Configures Identity to store its data in EF Core

// Configures policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("IsAdmin", policy => policy.RequireClaim("UserType", UserTypes.Admin))
    .AddPolicy("IsScheduler", policy => policy.RequireClaim("UserType", UserTypes.Scheduler))
    .AddPolicy("IsLecturer", policy => policy.RequireClaim("UserType", UserTypes.Lecturer))
    .AddPolicy("IsStudent", policy => policy.RequireClaim("UserType", UserTypes.Student))
    .AddPolicy("IsAdminOrScheduler", policy =>
        policy.RequireClaim("UserType", UserTypes.Admin, UserTypes.Scheduler))
    .AddPolicy("CanManageClasses", policy => policy.RequireClaim("UserType", UserTypes.Admin, UserTypes.Scheduler))
    .AddPolicy("CanViewStudentsLectures",
        policy => policy.RequireClaim("UserType", UserTypes.Admin, UserTypes.Scheduler))
    .AddPolicy("CanManageStudentsLectures",
        policy => policy.RequireClaim("UserType", UserTypes.Admin, UserTypes.Scheduler))
    .AddPolicy("CanDeleteStudentsLectures", policy => policy.RequireClaim("UserType", UserTypes.Admin))
    .AddPolicy("CanManageCourses", policy => policy.RequireClaim("UserType", UserTypes.Admin, UserTypes.Scheduler));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Initialize the database
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    dbInitializer.Initialize();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Thêm middleware xử lý status code
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    "dashboard",
    "dashboard/{action=Index}",
    new { controller = "Dashboard" });

app.MapControllerRoute(
    "default",
    "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.Run();
