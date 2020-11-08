using System;
using System.Collections.Generic;
using System.Text;

namespace Livestream.Net.Trovo.GraphQL
{
    public static class Queries
    {
        public static string GetChannelId(string channel) => $"{{\"query\": \"query {{getLiveInfo(params: {{userName: \\\"{channel}\\\", requireDecorations: false}}) {{channelInfo {{id}}}}}}\"}}";
        public static string GetToken(string pageId) => $"{{\"query\": \"query getToken($params: WSGetTokenReqInput) {{getToken(params: $params) {{token}}}}\", \"variables\": {{\"params\": {{\"subinfo\": {{\"page\": {{\"scene\": \"SCENE_CHANNEL\",\"pageID\": {pageId}}},\"streamerID\": 0}}}}}}}}";
    }
}
