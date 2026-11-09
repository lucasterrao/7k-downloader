using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public class CreatedJobs { public string[] ids; }
class NativeSmoke {
    static int Main(string[] args) {
        try { Run(args[0], args[1]).GetAwaiter().GetResult(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static async Task Run(string root, string url) {
        using (var backend = new Backend(root)) {
            await backend.Start();
            var state = await backend.Get<AppStatus>("status");
            if (!state.ready) throw new Exception("Componentes indisponíveis.");
            var preview = await backend.Post<Preview>("preview", new { urls = new[] { url } });
            if (String.IsNullOrEmpty(preview.title)) throw new Exception("Consulta sem título.");
            Console.WriteLine("Consulta pela integração C#: " + preview.title);
            var chosen=Path.Combine(root,"pasta-escolhida");
            var created = await backend.Post<CreatedJobs>("jobs", new DownloadOptions { urls = new[] { url }, mode = "audio", format = "mp3", bitrate = "192", destination = chosen });
            for (int i = 0; i < 180; i++) {
                state = await backend.Get<AppStatus>("status"); var job = state.jobs.First(j => j.id == created.ids[0]);
                if (job.status == "failed") throw new Exception(job.message);
                if (job.status == "completed") {
                    if (job.files.Length == 0 || job.files.Any(f => !File.Exists(f) || new FileInfo(f).Length == 0)) throw new Exception("Arquivo ausente.");
                    if (job.options.mode != "audio" || job.options.format != "mp3" || job.progress != 100) throw new Exception("Dados da tarefa inválidos.");
                    if(job.files.Any(f=>Path.GetDirectoryName(f)!=Path.GetFullPath(chosen))||Directory.GetDirectories(chosen).Length!=0)throw new Exception("Os arquivos devem ser salvos diretamente na pasta escolhida.");
                    await backend.Post("jobs/"+job.id+"/remove",new{});var after=await backend.Get<AppStatus>("status");if(after.jobs.Any(j=>j.id==job.id)||job.files.Any(f=>!File.Exists(f)))throw new Exception("Remover o histórico não pode apagar o arquivo.");
                    Console.WriteLine("Download MP3 nativo, pasta escolhida sem subpastas e remoção do histórico sem apagar mídia: OK."); return;
                }
                await Task.Delay(500);
            }
            throw new Exception("Tempo de teste excedido.");
        }
    }
}
