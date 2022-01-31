using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace MicMute.Controllers
{
    public class MicController : ApiController
    {
     
        // GET api/mic
        public string Get()
        {
            CentralUIDispatcher.ProcessRequest("Toggle");
            return "toggled";
        }

  

  
    }
}
