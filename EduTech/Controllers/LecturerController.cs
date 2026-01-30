using EduTech.Models;
using EduTech.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduTech.Controllers
{
    [Authorize]
    public class LecturerController : Controller
    {
        private readonly EduTechDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public LecturerController(EduTechDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;

        }

        // Hiển thị danh sách giảng viên
        [HttpGet]
        [Authorize(Policy = "CanViewStudentsLectures")]
        public async Task<IActionResult> Index()
        {
            var lectures = await _context.Users
                .Join(_context.UserClaims,
                    user => user.Id,
                    claim => claim.UserId,
                    (user, claim) => new { User = user, Claim = claim })
                .Where(x => x.Claim.ClaimType == "UserType" && x.Claim.ClaimValue == UserTypes.Lecturer)
                .Select(x => x.User)
                .AsNoTracking()
                .ToListAsync();

            return View("Index", lectures);
        }

        // Hiển danh sách lớp học mà giảng viên đó dạy
        [HttpGet]
        [Authorize(Policy = "IsLecturer")]
        public async Task<IActionResult> ClassesTeaching()
        {
            var lecturer = await _userManager.GetUserAsync(User);
            if (lecturer == null)
            {
                return Unauthorized();
            }

            // Fetch classes taught by the lecturer
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassSchedules)
                .Where(c => c.Status == ClassStatus.InProgress && c.Lecturers.Any(l => l.Id == lecturer.Id))
                .ToListAsync();

            var classesTeaching = new List<ClassesTeachingViewModel>();
            int scheduleDataId = 1;

            foreach (var aClass in classes)
            {
                var viewModel = new ClassesTeachingViewModel
                {
                    ClassId = aClass.Id,
                    ClassName = aClass.Name,
                    CourseName = aClass.Course.Name,
                    RoomNumber = aClass.RoomNumber,
                    Schedule = aClass.ClassSchedules,
                    ScheduleData = new List<ScheduleData>(),
                    Status = aClass.Status,
                    StartDate = aClass.StartDate.ToString("MM/dd/yyyy"),
                    EndDate = aClass.EndDate.ToString("MM/dd/yyyy")
                };

                // Generate ScheduleData based on ClassSchedule
                var startDate = aClass.StartDate.ToDateTime(TimeOnly.MinValue);
                var endDate = aClass.EndDate.ToDateTime(TimeOnly.MinValue);

                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    foreach (var cs in aClass.ClassSchedules)
                    {
                        if (date.DayOfWeek == cs.Day)
                        {
                            var startDateTime = date.Date.Add(cs.StartTime.ToTimeSpan());
                            var endDateTime = date.Date.Add(cs.EndTime.ToTimeSpan());

                            viewModel.ScheduleData.Add(new ScheduleData
                            {
                                Id = scheduleDataId++,
                                Subject = aClass.Name,
                                StartTime = startDateTime,
                                EndTime = endDateTime
                            });
                        }
                    }
                }

                classesTeaching.Add(viewModel);
            }

            return View("ClassesTeaching", classesTeaching);
        }

        // Class đang dạy của giảng viên
        [HttpGet]
        [Authorize(Policy = "IsLecturer")]
        public async Task<IActionResult> ClassesInProgress()
        {
            var lecturer = await _userManager.GetUserAsync(User);
            if (lecturer == null)
            {
                return Unauthorized();
            }

            // Fetch InProgress classes taught by the lecturer
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Where(c => c.Status == ClassStatus.InProgress && c.Lecturers.Any(l => l.Id == lecturer.Id))
                .ToListAsync();

            return View("ClassesInProgress", classes);
        }

        // Hiển thị lịch sử các lớp giảng dạy của giảng viên
        [HttpGet]
        [Authorize(Policy = "IsLecturer")]
        public async Task<IActionResult> ClassesHistory()
        {
            var lecturer = await _userManager.GetUserAsync(User);
            if (lecturer == null)
            {
                return Unauthorized();
            }

            // Fetch all classes taught by the lecturer
            var classes = await _context.Classes
                .Include(c => c.Course)
                .Include(c => c.ClassSchedules)
                .Where(c => c.Lecturers.Any(l => l.Id == lecturer.Id))
                .ToListAsync();

            // Define the priority order for ClassStatus
            var orderedStatuses = new List<ClassStatus>
            {
                ClassStatus.Archived,
                ClassStatus.PaymentPending,
                ClassStatus.InProgress,
                ClassStatus.Open,
                ClassStatus.Pending
            };

            // Group classes by status based on priority order
            var groupedClasses = classes
                .GroupBy(c => c.Status)
                .OrderBy(g => orderedStatuses.IndexOf(g.Key))
                .ToList();

            return View("ClassesHistory", groupedClasses);
        }

        // Hiển thị form thêm giảng viên
        [HttpGet]
        [Authorize(Policy = "CanManageStudentsLectures")]
        public IActionResult Add()
        {
            return View(new LecturerViewModel());
        }

        // Thêm giảng viên
        [HttpPost]
        [Authorize(Policy = "CanManageStudentsLectures")]
        public async Task<IActionResult> Add(LecturerViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Name = model.Name,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserType = UserTypes.Lecturer,
                    // Tạm thời set EmailConfirmed = true để không cần xác nhận email
                    EmailConfirmed = true

                };
                if (string.IsNullOrEmpty(model.Password))
                {
                    ModelState.AddModelError(string.Empty, "Mật khẩu là bắt buộc.");
                    return View(model);
                }
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddClaimAsync(user, new Claim("UserType", UserTypes.Lecturer));
                    return RedirectToAction("Index");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "CanManageStudentsLectures")]
        public async Task<IActionResult> Edit(string id)
        {
            var lecturer = await _userManager.FindByIdAsync(id);
            if (lecturer == null)
            {
                return NotFound();
            }

            var viewModel = new LecturerViewModel
            {
                Id = lecturer.Id,
                Name = lecturer.Name ?? string.Empty,
                Email = lecturer.Email ?? string.Empty,
                PhoneNumber = lecturer.PhoneNumber ?? string.Empty
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Policy = "CanManageStudentsLectures")]
        public async Task<IActionResult> Edit(LecturerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.Id == null)
            {
                return NotFound();
            }

            // Update existing lecturer
            var existingLecturer = await _userManager.FindByIdAsync(model.Id);
            if (existingLecturer == null)
            {
                return NotFound();
            }
            var lecturer = existingLecturer;

            lecturer.Name = model.Name;
            lecturer.Email = model.Email;
            lecturer.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.UpdateAsync(lecturer);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = "CanDeleteStudentsLectures")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var lecturer = await _userManager.FindByIdAsync(id);
            if (lecturer == null)
            {
                return NotFound();
            }
            _context.Users.Remove(lecturer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Xem form Chấm điểm cho sinh viên
        [HttpGet]
        [Authorize(Policy = "IsLecturer")]
        public async Task<IActionResult> Grade(int classId, string studentId)
        {
            var model = new GradeViewModel
            {
                ClassId = classId,
                StudentId = studentId,
                AssignmentType = AssignmentType.Practice
            };

            return View(model);
        }

        // Chấm điểm cho sinh viên
        [HttpPost]
        [Authorize(Policy = "IsLecturer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Grade(GradeViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Verify that the lecturer is teaching this class
                var lecturerId = _userManager.GetUserId(User);
                var isLecturerAssigned = await _context.Classes
                    .AnyAsync(c => c.Id == model.ClassId && c.Lecturers.Any(l => l.Id == lecturerId));

                if (!isLecturerAssigned)
                {
                    return Forbid();
                }

                // Check if the grade already exists
                var existingGrade = await _context.StudentGrades
                    .FirstOrDefaultAsync(g =>
                        g.ClassId == model.ClassId &&
                        g.StudentId == model.StudentId &&
                        g.AssignmentType == model.AssignmentType);

                if (existingGrade != null)
                {
                    // Update existing grade
                    existingGrade.Score = model.Score;
                    existingGrade.Comments = model.Comments;
                }
                else
                {
                    // Create a new grade
                    var studentGrade = new StudentGrade
                    {
                        ClassId = model.ClassId,
                        StudentId = model.StudentId,
                        AssignmentType = model.AssignmentType,
                        Score = model.Score,
                        Comments = model.Comments
                    };
                    _context.StudentGrades.Add(studentGrade);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction("ClassList", "Class", new { id = model.ClassId });
            }

            return View(model);
        }

       

    }
}
