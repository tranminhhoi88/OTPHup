
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace OTPHup
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddSignalR();
            builder.Services.AddCors(options => options.AddDefaultPolicy(builder =>
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
            var app = builder.Build();
            app.UseCors();
            app.UseRouting();


            // **L?u OTP theo userId và sessionId**
            var otpStore = new ConcurrentDictionary<string, string>();

            // **1. Nh?n OTP t? Android và l?u theo userId + sessionId**
            app.MapPost("/receive-otp", async (HttpContext context) =>
            {
                var request = await context.Request.ReadFromJsonAsync<OtpRequest>();
                if (request?.UserId == null || request.SessionId == null || request.Otp == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid userId, sessionId, or OTP.");
                    return;
                }

                string key = $"{request.UserId}:{request.SessionId}";
                otpStore[key] = request.Otp; // L?u OTP theo userId và sessionId

                var hubContext = app.Services.GetRequiredService<IHubContext<OtpHub>>();
                await hubContext.Clients.Group(key).SendAsync("ReceiveOtp", request.Otp);

                await context.Response.WriteAsJsonAsync(new { status = "OTP received", userId = request.UserId, sessionId = request.SessionId });
            });

            // **2. ?ng d?ng Console/WinForm l?y OTP theo userId & sessionId**
            app.MapGet("/get-otp/{userId}/{sessionId}", async (HttpContext context, string userId, string sessionId) =>
            {
                string key = $"{userId}:{sessionId}";
                if (otpStore.TryGetValue(key, out var otp))
                    await context.Response.WriteAsJsonAsync(new { otp });
                else
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("OTP not found.");
                }
            });

            // **3. WebSocket Server ?? g?i OTP ngay l?p t?c**
            app.MapHub<OtpHub>("/otp-hub");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            app.Run($"http://0.0.0.0:{port}");
           // app.Run();
        }
    }
    public class OtpRequest
    {
        public string UserId { get; set; }
        public string SessionId { get; set; }
        public string Otp { get; set; }
    }
    public class OtpHub : Hub
    {
        public async Task JoinSession(string userId, string sessionId)
        {
            string key = $"{userId}:{sessionId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, key);
        }
    }
}
