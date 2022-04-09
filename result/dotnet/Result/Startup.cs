using Dapper;
using System.Linq;
using System.Timers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace Result {

    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
                
        public void ConfigureServices(IServiceCollection services) {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddSignalR();

            services.AddTransient<IResultData, MySqlResultData>()
                    .AddSingleton<PublishResultsTimer>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseSignalR(routes =>
            {
                routes.MapHub<ResultsHub>("/resultsHub");
            });
            app.UseMvc();

            var timer = app.ApplicationServices.GetService<PublishResultsTimer>();
            timer.Start();
        }
    }

    // Data
    public class ResultsModel {
        public int OptionA { get; set; }
        public int OptionB { get; set; }
        public int VoteCount { get; set; }
    }
    
    public interface IResultData {
        ResultsModel GetResults();
    }

    public class MySqlResultData : IResultData {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public MySqlResultData(IConfiguration config, ILogger<MySqlResultData> logger) {
            _connectionString = config.GetConnectionString("ResultData");
            _logger = logger;
        }

        public ResultsModel GetResults() {
            var model = new ResultsModel();            
            using (var connection = new MySqlConnection(_connectionString)) {
                var results = connection.Query("SELECT vote, COUNT(id) AS count FROM votes GROUP BY vote ORDER BY vote");
                if (results.Any(x => x.vote == "a")) {
                    model.OptionA = (int) results.First(x => x.vote == "a").count;
                }
                if (results.Any(x => x.vote == "b")) {
                    model.OptionB = (int) results.First(x => x.vote == "b").count;
                }
                model.VoteCount = model.OptionA + model.OptionB;
            }
            return model;
        }
    }

    // Hub
    public class ResultsHub : Hub { }

    // Timer
    public class PublishResultsTimer {        
        private readonly IHubContext<ResultsHub> _hubContext;
        private readonly IResultData _resultData;
        private readonly Timer _timer;

        public PublishResultsTimer(IHubContext<ResultsHub> hubContext, IResultData resultData, IConfiguration configuration) {
            _hubContext = hubContext;
            _resultData = resultData;
            var publishMilliseconds = configuration.GetValue<int>("ResultsTimer:PublishMilliseconds");
            _timer = new Timer(publishMilliseconds)
            {
                Enabled = false
            };
            _timer.Elapsed += PublishResults;
        }

        public void Start() {
            if (!_timer.Enabled) {
                _timer.Start();
            }
        }

        private void PublishResults(object sender, ElapsedEventArgs e) {
            var model = _resultData.GetResults();
            _hubContext.Clients.All.SendAsync("UpdateResults", model);
        }
    }
}
