using System;
using System.Collections.Generic;


namespace openrmf_templates_api.Models
{

    public class CHECKLIST {

        public CHECKLIST (){
            ASSET = new ASSET();
            STIGS = new STIGS();
        }

        public ASSET ASSET { get; set; }
        public STIGS STIGS { get; set; }
    }
}