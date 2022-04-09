using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client;
using Newtonsoft.Json;

namespace Vote {

    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services) {
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
                });
            services.AddTransient<IMessageQueue, MessageQueue>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Error");
            }
                                 
            app.UseStaticFiles();
            app.UseMvc();
        }
    }

    public abstract class Message {
        public string CorrelationId { get; set; }  
        public abstract string Subject { get; }      

        public Message() {
            CorrelationId = Guid.NewGuid().ToString();
        }
    }
    
    public class VoteCastEvent : Message {
        public override string Subject { get { return MessageSubject; } }
        public string VoterId {get; set;}
        public string Vote {get; set; }
        public static string MessageSubject = "events.vote.votecast";
    }

    public class MessageHelper {
        public static byte[] ToData<TMessage>(TMessage message) where TMessage : Message {
            var json = JsonConvert.SerializeObject(message);
            return Encoding.Unicode.GetBytes(json);
        }

        public static TMessage FromData<TMessage>(byte[] data) where TMessage : Message {
            var json = Encoding.Unicode.GetString(data);
            return (TMessage)JsonConvert.DeserializeObject<TMessage>(json);
        }
    }

    public interface IMessageQueue {
        IConnection CreateConnection();
        void Publish<TMessage>(TMessage message) where TMessage : Message;
    }

    public class MessageQueue : IMessageQueue {
        protected readonly IConfiguration _configuration;
        protected readonly ILogger _logger;

        public MessageQueue(IConfiguration configuration, ILogger<MessageQueue> logger) {
            _configuration = configuration;
            _logger = logger;
        }

        public void Publish<TMessage>(TMessage message) where TMessage : Message {
            using (var connection = CreateConnection()) {
                var data = MessageHelper.ToData(message);
                connection.Publish(message.Subject, data);
            }
        }

        public IConnection CreateConnection() {
            var url = _configuration.GetValue<string>("MessageQueue:Url");
            return new ConnectionFactory().CreateConnection(url);
        }
    }
}
