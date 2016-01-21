﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Roboto
{
    public class ExpectedReply
    {
        /// <summary>
        /// The chat that the reply relates to (not the chat it was posted to, neccessarily). 
        /// </summary>
        public long chatID = -1;
        public long userID = -1;
        public bool isPrivateMessage = false;
        public DateTime timeLogged = DateTime.Now;
        public DateTime timeSentToUser = DateTime.MinValue;
        public string text = "";
        public long replyToMessageID = -1;
        public bool selective = false;
        public string keyboard = "";
        /// <summary>
        /// Internal data that can be returned to the plugin after the response is recieved
        /// </summary>
        public string messageData;
        public String pluginType;
        public int outboundMessageID;
        

        internal ExpectedReply() { }
        
        /// <summary>
        /// An outbound message that is logged on a stack, so that we can properly direct the reply, and send any further replies in sequence. 
        /// </summary>
        /// <param name="c"></param>
        /// <param name="userID"></param>
        /// <param name="text"></param>
        /// <param name="isPrivateMessage"></param>
        /// <param name="pluginType"></param>
        /// <param name="messageData"></param>
        public ExpectedReply(long chatID, long userID, string text, bool isPrivateMessage, Type pluginType, string messageData, long replyToMessageID, bool selective, string keyboard)
        {
            this.chatID = chatID;
            this.userID = userID;
            this.text = text;
            this.isPrivateMessage = isPrivateMessage;
            this.messageData = messageData;
            this.pluginType = pluginType.ToString();
            this.replyToMessageID = replyToMessageID;
            this.selective = selective;
            this.keyboard = keyboard;

        }

        /// <summary>
        /// Has the message been sent to the user?
        /// </summary>
        /// <returns></returns>
        public bool isSent()
        {
            if (timeSentToUser != DateTime.MinValue) { return true; }
            return false;
        }

        /// <summary>
        /// Check if this is of the right type. 
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool isOfType(Type t)
        {
            if (t.ToString() == pluginType)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Send the message
        /// </summary>
        /// <returns></returns>
        public int sendMessage()
        {

            outboundMessageID = TelegramAPI.postExpectedReplyToPlayer(this);
            timeSentToUser = DateTime.Now;

            return outboundMessageID;
        }
           

    }
}
