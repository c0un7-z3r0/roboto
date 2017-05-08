﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Roboto.Modules
{

    /*
    Moved to settings / telegramAPI
    /// <summary>
    /// Represents a reply we are expecting
    /// TODO - maybe genericise this?
    /// </summary>
    public class mod_xyzzy_expectedReply
    {
        public int messageID;
        public int chatID;
        public int playerID;
        public string replyData; //somewhere to store stuff about the reply
        internal mod_xyzzy_expectedReply() { }
        public mod_xyzzy_expectedReply(int messageID, int playerID, int chatID, string replyData)
        {
            this.messageID = messageID;
            this.playerID = playerID;
            this.chatID = chatID;
            this.replyData = replyData;
        }
    }
    */

    /// <summary>
    /// Represents a xyzzy player
    /// </summary>
    public class mod_xyzzy_player
    {
        public string name;

        public string name_markdownsafe
        {
            get
            {
                return Helpers.common.removeMarkDownChars(name);
            }
        }


        public bool fuckedWith = false;
        public string handle = "";
        public long playerID;
        public int wins = 0;
        public List<String> cardsInHand = new List<string>();
        public List<String> selectedCards = new List<string>();
        internal mod_xyzzy_player() { }
        public mod_xyzzy_player(string name, string handle, long playerID)
        {
            this.name = name;
            this.handle = handle;
            this.playerID = playerID;
        }

        public override string ToString()
        {

            string response = " " + name;
            if (handle != "") { response += " (@" + handle + ")"; }

            return response;
        }

        internal void topUpCards(int nrCards, List<string> availableAnswers, long chatID)
        {
            
            while (cardsInHand.Count < nrCards)
            {
                //have we reached the end of the pack?
                if (availableAnswers.Count == 0)
                {
                    //get the chatData and top up the cards. 
                    mod_xyzzy_chatdata chatData = (mod_xyzzy_chatdata)Roboto.Settings.getChat(chatID).getPluginData(typeof(mod_xyzzy_chatdata));
                    chatData.addAllAnswers();
                    TelegramAPI.SendMessage(chatID, "All answers have been used up, pack has been refilled!");
                }

                //pick a card
                string cardUID = availableAnswers[settings.getRandom(availableAnswers.Count)];
                cardsInHand.Add(cardUID);

                //remove it from the available list
                availableAnswers.Remove(cardUID);
            }
        }



        public bool SelectAnswerCard(string cardUID)
        {
            bool success = cardsInHand.Remove(cardUID);
            if (success)
            {
                selectedCards.Add(cardUID);
            }
            return success;

        }

        public string getAnswerKeyboard(mod_xyzzy_coredata localData)
        {
            List<string> answers = new List<string>();

            List<string> invalidCards = new List<string>();
            foreach (string cardID in cardsInHand)
            {
                mod_xyzzy_card c = localData.getAnswerCard(cardID);
                if (c != null)
                {

                   answers.Add(c.text);
                }
                else
                {
                    Roboto.log.log("Answer card " + cardID + " not found! Removing from " + name + "'s hand", logging.loglevel.critical);
                    invalidCards.Add(cardID);
                }
            }
            //remove any invalid cards
            foreach(string cardID in invalidCards) { cardsInHand.Remove(cardID); }

            return (TelegramAPI.createKeyboard(answers,1));
         }

        public void toggleFuckWith()
        {

            if (fuckedWith == true) { fuckedWith = false; }
            else { fuckedWith = true; }
        }

        public string getPointsMessage()
        {
            
            string response = "\n\r" + name_markdownsafe + " - ";
            if (!fuckedWith) { return response + wins + " points."; }
            else
            {
                string[] suffixes = { "INT", "XP", "Points", "Sq. Ft.", "ft, 6 inches", "mm", " out of 10. Must try harder." };

                //want a multipler between -1 and 0.5.
                float multiplier = (50 - settings.getRandom(150))/100f ;
                int randomSuffix = settings.getRandom(suffixes.Count() - 1);
                int newscore = Convert.ToInt32(wins * multiplier);
                response += newscore.ToString() + " " + suffixes[randomSuffix];
                
            }
            return response;
        }
    }


    /// <summary>
    /// Represents a xyzzy card
    /// </summary>
    public class mod_xyzzy_card
    {
        public string uniqueID = Guid.NewGuid().ToString();
        public String text;
        [System.Obsolete("use Pack (Guid)")]
        public String category; //what pack did the card come from

        //shitty workaround to allow us to load in the cateogry info temporarily. - http://stackoverflow.com/questions/5096926/what-is-the-get-set-syntax-in-c
        [XmlElement("category")]
        public string TempCategory
        {
            #pragma warning disable 612, 618
            get { return category; }
            set { category = value; }
            #pragma warning restore 612, 618
        }

        public Guid packID;
        public int nrAnswers = -1; 

        internal mod_xyzzy_card() { }
        public mod_xyzzy_card(String text, Guid packID, int nrAnswers = -1)
        {
            this.text = text;
            this.packID = packID;
            this.nrAnswers = nrAnswers;
        }

    }


    /// <summary>
    /// The XXZZY Plugin
    /// </summary>
    public class mod_xyzzy : RobotoModuleTemplate
    {
        public static Guid primaryPackID = new Guid("FACEBABE-DEAD-BEEF-ABBA-FACEBABEFADE");
        public static Guid AllPacksEnabledID = Guid.Empty;

        private mod_xyzzy_coredata localData;
        

        public override void init()
        {
            pluginDataType = typeof(mod_xyzzy_coredata);
            pluginChatDataType = typeof(mod_xyzzy_chatdata);

            chatHook = true;
            chatEvenIfAlreadyMatched = false;
            chatPriority = 3;

            backgroundHook = true;
            backgroundMins = 1; //every 1 min, check the latest 20 chats
            
        }

        public override string getMethodDescriptions()
        {
            return
                "xyzzy_start - Starts a new game of xyzzy with the players in the chat" + "\n\r" +
                "xyzzy_settings - Change the various game settings" + "\n\r" +
                "xyzzy_get_settings - Get the current game settings" + "\n\r" +
                "xyzzy_join - Join a game of xyzzy that is in progress, or about to start" + "\n\r" +
                "xyzzy_leave - Leave a game of xyzzy" + "\n\r" +
                //"xyzzy_extend - Extends a running game with more cards, or restarts a game that has just stopped" + "\n\r" +
                "xyzzy_status - Gets the current status of the game";
                //"xyzzy_filter - Shows the filters and their current status" + "\n\r" +
        }

        public override string getWelcomeDescriptions()
        {
            return "To start a new game of Chat Against Humanity, type /xyzzy_start in a group chat window. You'll need a couple of friends, and you will all need to open a private message session with the bot to play." + "\n\r" + 
                "Chat Against Humanity is a virtual card game you can play with friends over Telegram! Each round, the dealer will ask a question, players will get a private message where they should pick their best answer card from a list of cards in their hand, and the dealer can judge the best answer from the replies.";

        }

        public override void initData()
        {
            try
            {
                localData = Roboto.Settings.getPluginData<mod_xyzzy_coredata>();
            }
            catch (InvalidDataException)
            {
                //Data doesnt exist, create, populate with sample data and register for saving
                localData = new mod_xyzzy_coredata();
                sampleData();
                Roboto.Settings.registerData(localData);
            }

            Roboto.Settings.stats.registerStatType("New Games Started", this.GetType(), System.Drawing.Color.Aqua);
            Roboto.Settings.stats.registerStatType("Games Ended", this.GetType(), System.Drawing.Color.Orange);
            Roboto.Settings.stats.registerStatType("Hands Played", this.GetType(), System.Drawing.Color.Olive);
            Roboto.Settings.stats.registerStatType("Packs Synced", this.GetType(), System.Drawing.Color.DarkBlue );
            Roboto.Settings.stats.registerStatType("Bad Responses", this.GetType(), System.Drawing.Color.Olive);
            Roboto.Settings.stats.registerStatType("Active Games", this.GetType(), System.Drawing.Color.Green, stats.displaymode.line, stats.statmode.absolute);
            Roboto.Settings.stats.registerStatType("Active Players", this.GetType(), System.Drawing.Color.Blue, stats.displaymode.line, stats.statmode.absolute);
            Roboto.Settings.stats.registerStatType("Background Wait", this.GetType(), System.Drawing.Color.Red, stats.displaymode.line, stats.statmode.absolute);
            Roboto.Settings.stats.registerStatType("Background Wait (Quickcheck)", this.GetType(), System.Drawing.Color.Red, stats.displaymode.line, stats.statmode.absolute);
            Roboto.Settings.stats.registerStatType("Background Wait (Pack Sync)", this.GetType(), System.Drawing.Color.Cyan, stats.displaymode.line, stats.statmode.absolute);

            Console.WriteLine(localData.questions.Count.ToString() + " questions and " + localData.answers.Count.ToString() + " answers loaded for xyzzy");

        }

        public override void initChatData(chat c)
        {
            mod_xyzzy_chatdata chatData = c.getPluginData<mod_xyzzy_chatdata>();
           
            if (chatData == null)
            {
                //Data doesnt exist, create, populate with sample data and register for saving
                chatData = new mod_xyzzy_chatdata();
                c.addChatData(chatData);
            }
        }

        /// <summary>
        /// Process chat messages
        /// </summary>
        /// <param name="m"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public override bool chatEvent(message m, chat c = null)
        {
            //Various bits of setup before starting to process the message
            bool processed = false;
            
            if (c != null) //Setup needs to be done in a chat! Other replies will now have a chat object passed in here too!
            {
                //get current game data. 
                mod_xyzzy_chatdata chatData = c.getPluginData<mod_xyzzy_chatdata>();

                if (chatData == null)
                {
                    log("Chatdata doesnt exist! creating.", logging.loglevel.high);
                    initChatData(c);
                    chatData = c.getPluginData<mod_xyzzy_chatdata>();

                    if (chatData == null)
                    {
                        log("Chatdata still doesnt exist! creating.", logging.loglevel.critical);
                    }
                }

                if (m.text_msg.StartsWith("/xyzzy_start") && chatData.status == xyzzy_Statuses.Stopped)
                {

                    Roboto.Settings.stats.logStat(new statItem("New Games Started", this.GetType()));
                    //Start a new game!

                    //try and send the opening message


                    //use defaults or configure game
                    string kb = TelegramAPI.createKeyboard(new List<string>() { "Use Defaults", "Configure Game", "Cancel" }, 2);
                    
                    long messageID = TelegramAPI.GetExpectedReply(c.chatID, m.userID, "Do you want to start the game with the default settings, or set advanced optons first? You can change these options later with /xyzzy_settings", true, typeof(mod_xyzzy), "useDefaults",-1,false,kb);

                    if (messageID == long.MinValue)
                    {
                        //no private message session
                        TelegramAPI.SendMessage(m.chatID, m.userFullName + " needs to open a private chat to @" +
                            Roboto.Settings.botUserName + " to be able to start a game", false, -1, true);
                    }
                    else
                    {
                        //message went out successfully, start setting it up proper
                        chatData.reset();
                        //Roboto.Settings.clearExpectedReplies(c.chatID, typeof(mod_xyzzy)); //Cant do this, as clears the "how many questions" we just asked!
                        chatData.setStatus(xyzzy_Statuses.useDefaults);
                        //add the player that started the game
                        chatData.addPlayer(new mod_xyzzy_player(m.userFullName, m.userHandle, m.userID));


                        //send out invites
                        TelegramAPI.SendMessage(m.chatID, m.userFullName + " is starting a new game of xyzzy! Type /xyzzy_join to join. You can join / leave " +
                            "at any time - you will be included next time a question is asked. You will need to open a private chat to @" +
                            Roboto.Settings.botUserName + " if you haven't got one yet - unfortunately I am a stupid bot and can't do it myself :("
                            , false, -1, true);
                    }
                    
                    //TODO - wrap the TelegramAPI calls into methods in the plugin and pluginData classes.                    
                    
                }
                //Start but there is an existing game
                else if (m.text_msg.StartsWith("/xyzzy_start"))
                {
                    chatData.getStatus();
                    processed = true;
                }


                //player joining
                else if (m.text_msg.StartsWith("/xyzzy_join") && chatData.status != xyzzy_Statuses.Stopped)
                {
                    //try send a test message. If it fails, tell the user to open a 1:1 chat.
                    long i = -1;
                    try
                    {
                        i = TelegramAPI.SendMessage(m.userID, "You joined the xyzzy game in " + m.chatName);
                        if (i == -1)
                        {
                            log("Adding user, but the outbound message is still queued", logging.loglevel.verbose);
                            TelegramAPI.SendMessage(m.chatID, "Sent " + m.userFullName + " a message, but I'm waiting for them to reply to another question. "
                                + m.userFullName + " is in the game, but will need to clear their other PMs before they see any questions. ", false, m.message_id);

                        }
                        else if (i < 0)
                        {
                            log("Adding user, but message blocked, abandoning", logging.loglevel.warn);
                            TelegramAPI.SendMessage(m.chatID, "Couldn't add " + m.userFullName + " to the game, as I couldnt send them a message. "
                               + m.userFullName + " probably needs to open a chat session with me. "
                               + "Create a message session, then try /xyzzy_join again.", false, m.message_id);
                        }

                    
                        if (i != long.MinValue) //if we didnt get an error sending the message
                        {
                            log("Adding user processing", logging.loglevel.verbose);
                            bool added = chatData.addPlayer(new mod_xyzzy_player(m.userFullName, m.userHandle, m.userID));
                            if (added) { TelegramAPI.SendMessage(c.chatID, m.userFullName + " has joined the game"); }
                            else { TelegramAPI.SendMessage(c.chatID, m.userFullName + " is already in the game"); }
                        }
                    }
                    catch
                    {
                        //shouldnt actually get here. "Normal" errors should result in a -1 (queued) or a minvalue (forbidden)
                        log("Other excpetion sending add player confirmation message", logging.loglevel.high);
                    }
                    processed = true;
                }
                //Start but there is an existing game
                else if (m.text_msg.StartsWith("/xyzzy_join"))
                {
                    chatData.getStatus();
                    processed = true;
                }

                //player leaving
                else if (m.text_msg.StartsWith("/xyzzy_leave"))
                {
                    bool removed = chatData.removePlayer(m.userID);
                    //if (removed) { TelegramAPI.SendMessage(c.chatID, m.userFullName + " has left the game"); }
                    //else { TelegramAPI.SendMessage(c.chatID, m.userFullName + " isnt part of the game, and can't be removed!"); }
                    processed = true;
                }
               
                else if (m.text_msg.StartsWith("/xyzzy_status"))
                {
                    chatData.getStatus();
                    processed = true;
                }
                else if (m.text_msg.StartsWith("/xyzzy_settings"))
                {
                    chatData.sendSettingsMessage(m);
                    processed = true;
                }
                else if (m.text_msg.StartsWith("/xyzzy_get_settings"))
                {
                    chatData.sendSettingsMsgToChat();
                    processed = true;
                }


                /*Moved to the /xyzzy_settings command
                else if (m.text_msg.StartsWith("/xyzzy_setFilter"))
                {
                    chatData.sendSettingsMessage(m);
                    processed = true;
                }

                
                else if (m.text_msg.StartsWith("/xyzzy_filter"))
                {
                    string response = "The following pack filters are currently set. These can be changed when starting a new game : " + "\n\r" +
        chatData.getPackFilterStatus();
                    TelegramAPI.SendMessage(m.chatID, response, false, m.message_id);
                    processed = true;
                }
                //set the filter (inflight)
                else if (m.text_msg.StartsWith("/xyzzy_setFilter"))
                {
                    chatData.sendPackFilterMessage(m, 1);
                    
                    processed = true;
                }
                //set the filter (inflight)
                else if (m.text_msg.StartsWith("/xyzzy_reDeal"))
                {
                    TelegramAPI.SendMessage(m.chatID, "Resetting everyone's cards, and shuffled the decks", false, m.message_id);
                    chatData.reDeal();
                    
                    processed = true;
                }

                else if (m.text_msg.StartsWith("/xyzzy_reset"))
                {
                    chatData.resetScores();
                    TelegramAPI.SendMessage(m.chatID, "Scores have been reset!", false, m.message_id);
                    processed = true;
                }

                //inflite options
                else if (m.text_msg.StartsWith("/xyzzy_setTimeout"))
                {
                    chatData.askMaxTimeout(m.userID);
                }
                else if (m.text_msg.StartsWith("/xyzzy_setThrottle"))
                {
                    chatData.askMinTimeout(m.userID);
                }
                */
            }
            //has someone tried to do something unexpected in a private chat?
            else if (m.chatID == m.userID && m.text_msg.StartsWith("/xyzzy_"))
            {
                TelegramAPI.SendMessage(m.chatID, "To start a game, add me to a group chat, and type /xyzzy_start");
                processed = true;
            }


            return processed;
        }



        protected override void backgroundProcessing()
        {
            mod_xyzzy_coredata localdata = (mod_xyzzy_coredata)getPluginData();

            //update stats
            int activeGames = 0;
            int activePlayers = 0;
            foreach (chat c in Roboto.Settings.chatData)
            {
                mod_xyzzy_chatdata chatData = c.getPluginData<mod_xyzzy_chatdata>();
                if (chatData != null && chatData.status != xyzzy_Statuses.Stopped)
                {
                    activeGames++;
                    activePlayers += chatData.players.Count;
                }
            }
            Roboto.Settings.stats.logStat(new statItem("Active Games", this.GetType(), activeGames));
            Roboto.Settings.stats.logStat(new statItem("Active Players", this.GetType(), activePlayers));


            //sync packs where needed
            localdata.packSyncCheck();
            
            //Handle background processing per chat (Timeouts / Throttle etc..)
            //create a temporary list of chatdata so we can pick the oldest X records
            List<mod_xyzzy_chatdata> dataToCheck = new List<mod_xyzzy_chatdata>();
            List<mod_xyzzy_chatdata> dataToMiniCheck = new List<mod_xyzzy_chatdata>();

            foreach (chat c in Roboto.Settings.chatData)
            {
                mod_xyzzy_chatdata chatData = (mod_xyzzy_chatdata)c.getPluginData<mod_xyzzy_chatdata>();
                if (chatData != null && chatData.status != xyzzy_Statuses.Stopped)
                { 
                    //do a full check at most once per day
                    if (chatData.statusCheckedTime < DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0)))
                    {
                        dataToCheck.Add(chatData);
                    }
                    //do a mini check at most every 5 mins
                    if (chatData.statusMiniCheckedTime < DateTime.Now.Subtract(new TimeSpan(0,0,5,0)))
                    {
                        dataToMiniCheck.Add(chatData);
                    }
                }
            }

            log("There are " + dataToCheck.Count() + " games to check. Checking oldest " + localdata.backgroundChatsToProcess , logging.loglevel.normal);

            //do a full check on the oldest 20 records. Dont check more than once per day. 
            bool firstrec = true;
            foreach (mod_xyzzy_chatdata chatData in dataToCheck.OrderBy(x => x.statusCheckedTime).Take(localdata.backgroundChatsToProcess))
            {
                if (firstrec)
                {
                    log("Oldest chat was last checked " + Convert.ToInt32(DateTime.Now.Subtract(chatData.statusCheckedTime).TotalMinutes) + " minute(s) ago", logging.loglevel.low);
                    Roboto.Settings.stats.logStat(new statItem("Background Wait", this.GetType(), Convert.ToInt32(DateTime.Now.Subtract(chatData.statusCheckedTime).TotalMinutes)));
                }
                chatData.check(true);
                firstrec = false;
            }

            
            //also do a quick check on the oldest 100 ordered by statusMiniCheckTime
            log("There are " + dataToMiniCheck.Count() + " games to quick-check. Checking oldest " + localdata.backgroundChatsToMiniProcess, logging.loglevel.normal);
            firstrec = true;
            foreach (mod_xyzzy_chatdata chatData in dataToMiniCheck.OrderBy(x => x.statusMiniCheckedTime).Take(localdata.backgroundChatsToMiniProcess))
            {
                if (firstrec)
                {
                    log("Oldest chat was last quick-checked " + Convert.ToInt32(DateTime.Now.Subtract(chatData.statusMiniCheckedTime).TotalMinutes) + " minute(s) ago", logging.loglevel.low);
                    Roboto.Settings.stats.logStat(new statItem("Background Wait (Quickcheck)", this.GetType(), Convert.ToInt32(DateTime.Now.Subtract(chatData.statusMiniCheckedTime).TotalMinutes)));
                }
                chatData.check();
                firstrec = false;
            }
        }

        public override string getStats()
        {
            int activePlayers = 0;
            int activeGames = 0;
            int dormantGames = 0;

            foreach (chat c in Roboto.Settings.chatData)
            {
                mod_xyzzy_chatdata chatData = c.getPluginData<mod_xyzzy_chatdata>();
                if (chatData != null && chatData.status != xyzzy_Statuses.Stopped)
                {
                    activeGames++;
                    activePlayers += chatData.players.Count;
                    if (chatData.statusChangedTime < DateTime.Now.Subtract(new TimeSpan(30,0,0,0)) )
                    {
                        dormantGames++;
                    }
                }
            }

            log("There are " + dormantGames + " potentially cancelable games", logging.loglevel.normal);

            
            string result = activePlayers.ToString() + " players in " + activeGames.ToString() + " active games";

            return result;

        }

        public override bool replyReceived(ExpectedReply e, message m, bool messageFailed = false)
        {
            bool processed = false;
            chat c = Roboto.Settings.getChat(e.chatID);
            mod_xyzzy_chatdata chatData = c.getPluginData<mod_xyzzy_chatdata>();

            //did one of our outbound messages fail?
            if (messageFailed)
            {
                //TODO - better handling of failed outbound messages. Timeout player or something depending on status? 
                try
                {
                    string message = "Failed Incoming expected reply";
                    if (c != null) { message += " for chat " + c.ToString(); }
                    if (m != null) { message += " recieved from chatID " + m.chatID + " from userID " + m.userID + " in reply to " + e.outboundMessageID; }


                log(message, logging.loglevel.high);
                }
                catch (Exception ex)
                {
                    log("Error thrown during failed reply processing " + ex.ToString(), logging.loglevel.critical);
                }
                return true;
            }

            else
            {
                log("Incoming expected reply for chat " + c.ToString() + " recieved from chatID " + m.chatID + " from userID " + m.userID + " in reply to " + e.outboundMessageID, logging.loglevel.verbose);
            }

            //Set up the game, once we get a reply from the user. 
            if (e.messageData == "Settings")
            {
                if (m.text_msg == "Cancel") { } //do nothing, should just end and go back
                else if (m.text_msg == "Change Packs") { chatData.sendPackFilterMessage(m, 1); }
                else if (m.text_msg == "Re-deal") { chatData.reDeal(); }
                else if (m.text_msg == "Extend") { chatData.extend(); }
                else if (m.text_msg == "Reset") { chatData.reset(); }
                else if (m.text_msg == "Force Question") { chatData.forceQuestion(); }
                else if (m.text_msg == "Timeout") { chatData.askMaxTimeout(m.userID); }
                else if (m.text_msg == "Delay") { chatData.askMinTimeout(m.userID); }
                else if (m.text_msg == "Kick") { chatData.askKickMessage(m); }
                else if (m.text_msg == "Mess With") { chatData.askFuckWithMessage(m); }

                else if (m.text_msg == "Abandon")
                {
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, "Are you sure you want to abandon the game?", true, typeof(mod_xyzzy), "Abandon", -1, true, TelegramAPI.createKeyboard(new List<string>() { "Yes", "No" }, 2));
                }
                return true;
            }



            else if (e.messageData == "useDefaults")
            {
                if (m.text_msg == "Use Defaults")
                {
                    //add all the q's and a's based on the previous settings / defaults if a new game. 
                    chatData.addQuestions();
                    chatData.addAllAnswers();
                    string keyboard = TelegramAPI.createKeyboard(new List<string> { "Start", "Cancel" }, 2);
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, "To start the game once enough players have joined click the \"Start\" button below. You will need three or more players to start the game.", true, typeof(mod_xyzzy), "Invites", -1, true, keyboard);
                    chatData.setStatus(xyzzy_Statuses.Invites);
                }
                else if (m.text_msg == "Configure Game")
                {
                    TelegramAPI.GetExpectedReply(c.chatID, m.userID, "How many questions do you want the round to last for (-1 for infinite)", true, typeof(mod_xyzzy), "SetGameLength");
                    chatData.setStatus(xyzzy_Statuses.SetGameLength);
                }
                else if (m.text_msg == "Cancel")
                {

                    TelegramAPI.SendMessage(m.userID, "Cancelled setup");
                    chatData.setStatus(xyzzy_Statuses.Stopped);
                }
                else
                {
                    string kb = TelegramAPI.createKeyboard(new List<string>() { "Use Defaults", "Configure Game", "Cancel" }, 2);
                    long messageID = TelegramAPI.GetExpectedReply(c.chatID, m.userID, "Not a valid answer. Do you want to start the game with the default settings, or set advanced optons first? You can change these options later with /xyzzy_settings", true, typeof(mod_xyzzy), "useDefaults", -1, false, kb);
                }
                processed = true;
            }

            else if (e.messageData == "SetGameLength")
            {
                int questions;

                if (int.TryParse(m.text_msg, out questions) && questions >= -1)
                {
                    chatData.enteredQuestionCount = questions;
                    //next, ask which packs they want:
                    chatData.sendPackFilterMessage(m, 1);
                    chatData.setStatus(xyzzy_Statuses.setPackFilter);
                }
                else
                {
                    TelegramAPI.GetExpectedReply(c.chatID, m.userID, m.text_msg + " is not a valid number. How many questions do you want the round to last for? -1 for infinite", true, typeof(mod_xyzzy), "SetGameLength");
                }
                processed = true;
            }

            //Set up the game filter, once we get a reply from the user. 
            else if (e.messageData.StartsWith("setPackFilter"))
            {
                //figure out what page we are on. Should be in the message data
                int currentPage = 1;
                bool success = int.TryParse(e.messageData.Substring(14), out currentPage);
                if (!success)
                {
                    currentPage = 1;
                    log("Expected messagedata to contain a page number. Was " + e.messageData, logging.loglevel.high);
                }
                //import a cardcast pack
                if (m.text_msg == "Import CardCast Pack")
                {
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, Helpers.cardCast.boilerPlate + "\n\r"
                        + "To import a pack, enter the pack code. To cancel, type 'Cancel'", true, typeof(mod_xyzzy), "cardCastImport");
                    if (chatData.status == xyzzy_Statuses.setPackFilter) { chatData.setStatus(xyzzy_Statuses.cardCastImport); }
                }
                else if (m.text_msg == "Next")
                {
                    currentPage++;
                    chatData.sendPackFilterMessage(m, currentPage);

                }
                else if (m.text_msg == "Prev")
                {
                    currentPage--;
                    chatData.sendPackFilterMessage(m, currentPage);
                }

                //enable/disable an existing pack
                else if (m.text_msg != "Continue")
                {
                    chatData.processPackFilterMessage(m);
                    chatData.sendPackFilterMessage(m, currentPage);
                }
                //no packs selected, retry
                else if (chatData.packFilterIDs.Count == 0)
                {
                    chatData.sendPackFilterMessage(m, 1);
                }
                //This is presumably a continue now...
                else
                {
                    //are we adding this as part of the setup process?
                    if (chatData.status == xyzzy_Statuses.setPackFilter)
                    {
                        chatData.addQuestions();
                        chatData.addAllAnswers();

                        chatData.askMaxTimeout(m.userID);
                        chatData.setStatus(xyzzy_Statuses.setMaxHours);
                    }
                    else
                    {
                        //adding as part of a /settings. return to main
                        chatData.sendSettingsMessage(m);
                        //TelegramAPI.SendMessage(chatData.chatID, "Updated the pack list. New cards won't get added to the game until you restart, or /xyzzy_reDeal" );
                    }
                }
                processed = true;
            }


            //Cardcast importing
            else if (e.messageData == "cardCastImport")
            {
                if (m.text_msg == "Cancel")
                {
                    //return to plugins
                    chatData.sendPackFilterMessage(m, 1);
                    if (chatData.status == xyzzy_Statuses.cardCastImport) { chatData.setStatus(xyzzy_Statuses.setPackFilter); }
                }
                else
                {
                    string importMessage;
                    Helpers.cardcast_pack pack = new Helpers.cardcast_pack();
                    bool success = importCardCastPack(m.text_msg, out pack, out importMessage);
                    if (success == true)
                    {
                        //reply to user
                        TelegramAPI.SendMessage(m.userID, importMessage);
                        //enable the filter
                        chatData.processPackFilterMessage(m, pack.name);
                        //return to plugin selection
                        chatData.sendPackFilterMessage(m, 1);
                        if (chatData.status == xyzzy_Statuses.cardCastImport) { chatData.setStatus(xyzzy_Statuses.setPackFilter); }
                    }
                    else
                    {
                        TelegramAPI.GetExpectedReply(chatData.chatID, m.userID,
                        "Couldn't add the pack. " + importMessage + ". To import a pack, enter the pack code. To cancel, type 'Cancel'", true, typeof(mod_xyzzy), "cardCastImport");
                    }
                }
                processed = true;
            }

            //work out the maxWaitTime (timeout)
            else if (e.messageData == "setMaxHours")
            {
                //try parse
                bool success = chatData.setMaxTimeout(m.text_msg);
                if (success && chatData.status == xyzzy_Statuses.setMaxHours) //could be at another status if being set mid-game
                {
                    //move to the throttle
                    chatData.setStatus(xyzzy_Statuses.setMinHours);
                    chatData.askMinTimeout(m.userID);

                }
                else if (success)
                {
                    //success, called inflite
                    //TelegramAPI.SendMessage(e.chatID, "Set timeouts to " + (chatData.maxWaitTimeHours == 0 ? "No Timeout" : chatData.maxWaitTimeHours.ToString() + " hours") );
                    //adding as part of a /settings. return to main
                    chatData.sendSettingsMessage(m);

                }
                else {
                    //send message, and retry
                    TelegramAPI.SendMessage(m.userID, "Not a valid value!");
                    chatData.askMaxTimeout(m.userID);
                }
                processed = true;
            }

            //work out the minWaitTime (throttle)
            else if (e.messageData == "setMinHours")
            {
                //try parse
                bool success = chatData.setMinTimeout(m.text_msg);
                if (success && chatData.status == xyzzy_Statuses.setMinHours)//could be at another status if being set mid-game
                {

                    //Ready to start game - tell the player they can start when they want
                    string keyboard = TelegramAPI.createKeyboard(new List<string> { "Start", "Cancel" }, 2);
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, "To start the game once enough players have joined click the \"Start\" button below. You will need three or more players to start the game.", true, typeof(mod_xyzzy), "Invites", -1, true, keyboard);
                    chatData.setStatus(xyzzy_Statuses.Invites);

                }
                else if (success)
                {
                    //adding as part of a /settings. return to main
                    chatData.sendSettingsMessage(m);
                    //success, called inflite
                    //TelegramAPI.SendMessage(e.chatID, (chatData.minWaitTimeHours == 0 ? "Game throttling disabled" :  "Set throttle to only allow one round every " + chatData.minWaitTimeHours.ToString() + " hours"));
                }

                else
                {
                    //send message, and retry
                    TelegramAPI.SendMessage(m.userID, "Not a valid number!");
                    chatData.askMinTimeout(m.userID);
                }
                processed = true;
            }




            //start the game proper
            else if (chatData.status == xyzzy_Statuses.Invites && e.messageData == "Invites")
            // TBH, dont care what they reply with. Its probably "start" as thats whats on the keyboard, but lets not bother checking, 
            //as otherwise we would have to do some daft bounds checking 
            // && m.text_msg == "start")
            {
                if (m.text_msg == "Cancel")
                {
                    //allow player to cancel, otherwise the message just keeps coming back. 
                    chatData.setStatus(xyzzy_Statuses.Stopped);
                }
                else if (m.text_msg == "Override" && chatData.players.Count > 1)
                {
                    log("Overriding player limit and starting game", logging.loglevel.critical);
                    chatData.askQuestion(true);
                }
                else if (m.text_msg == "Start" && chatData.players.Count > 2)
                {
                    chatData.askQuestion(true);
                }
                else if (m.text_msg == "Start")
                {
                    string keyboard = TelegramAPI.createKeyboard(new List<string> { "Start", "Cancel" }, 2);
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, "Not enough players yet. You need three or more players to start the game. To start the game once enough players have joined click the \"Start\" button below.", true, typeof(mod_xyzzy), "Invites", -1, true, keyboard);
                }
                else
                {
                    string keyboard = TelegramAPI.createKeyboard(new List<string> { "Start", "Cancel" }, 2);
                    TelegramAPI.GetExpectedReply(chatData.chatID, m.userID, "To start the game once enough players have joined click the \"Start\" button below. You will need three or more players to start the game.", true, typeof(mod_xyzzy), "Invites", -1, true, keyboard);
                }

                processed = true;
            }

            //A player answering the question
            else if (chatData.status == xyzzy_Statuses.Question && e.messageData == "Question")
            {
                bool answerAccepted = chatData.logAnswer(m.userID, m.text_msg);
                processed = true;
                /*if (answerAccepted) - covered in the logAnswer step
                {
                    //no longer expecting a reply from this player
                    if (chatData.allPlayersAnswered())
                    {
                        chatData.beginJudging();
                    }
                }
                */
            }

            //A judges response
            else if (chatData.status == xyzzy_Statuses.Judging && e.messageData == "Judging" && m != null)
            {
                bool success = chatData.judgesResponse(m.text_msg);

                processed = true;
            }


            //abandon game
            else if (e.messageData == "Abandon")
            {
                chatData.setStatus(xyzzy_Statuses.Stopped);
                Roboto.Settings.clearExpectedReplies(c.chatID, typeof(mod_xyzzy));
                TelegramAPI.SendMessage(c.chatID, "Game abandoned. type /xyzzy_start to start a new game");
                processed = true;
            }



            //kicking a player
            else if (e.messageData == "kick")
            {
                mod_xyzzy_player p = chatData.getPlayer(m.text_msg);
                if (p != null)
                {
                    chatData.removePlayer(p.playerID);
                }
                chatData.check();
                //now return to the last settings page
                chatData.sendSettingsMessage(m);

                processed = true;
            }

            else if (e.messageData == "fuckwith")
            {
                mod_xyzzy_player p = chatData.getPlayer(m.text_msg);
                if (p != null)
                {
                    chatData.toggleFuckWith(p.playerID);
                }
                chatData.check();
                //now return to the last settings page
                chatData.sendSettingsMessage(m);

                processed = true;
            }
                


            return processed;
        }

        /// <summary>
        /// Import a cardcast pack into the xyzzy localdata
        /// </summary>
        /// <param name="packFilter"></param>
        /// <returns>String containing details of the pack and cards added. String will be empty if import failed.</returns>
        private bool importCardCastPack(string packCode, out Helpers.cardcast_pack pack, out string response)
        {

            bool success = localData.importCardCastPack(packCode, out pack, out response);
            
            return success;

        }

        public override void sampleData()
        {
            log("Adding stub sample packs");

            //Add packs for the standard CaH packs. These should be synced when we do startupChecks()
            Helpers.cardcast_pack primaryPack = new Helpers.cardcast_pack("Cards Against Humanity", "CAHBS", "Cards Against Humanity");
            primaryPack.overrideGUID(mod_xyzzy.primaryPackID); //override this one's guid so we can add it by default to new poacks. 

            localData.packs.Add(primaryPack);
            localData.packs.Add(new Helpers.cardcast_pack("Expansion 1 - CAH", "CAHE1", "Expansion 1 - CAH"));
            localData.packs.Add(new Helpers.cardcast_pack("Expansion 2 - CAH", "CAHE2", "Expansion 2 - CAH"));
            localData.packs.Add(new Helpers.cardcast_pack("Expansion 3 - CAH", "CAHE3", "Expansion 3 - CAH"));
            localData.packs.Add(new Helpers.cardcast_pack("Expansion 4 - CAH", "CAHE4", "Expansion 4 - CAH"));
            localData.packs.Add(new Helpers.cardcast_pack("CAH Fifth Expansion", "EU6CJ", "CAH Fifth Expansion"));
            localData.packs.Add(new Helpers.cardcast_pack("CAH Sixth Expansion", "PEU3Q", "CAH Sixth Expansion"));

        }

        /// <summary>
        /// Startup checks and housekeeping
        /// </summary>
        public override void startupChecks()
        {

            //make sure our local pack filter list is fully populated 
            localData.startupChecks();


            //TODO - some other mechanism for removing duplicate cards if we e.g. merge packs. Can't dedupe properly as e.g. John Cena pack has multiple cards

            //todo - this should be a general pack remove option
            //DATAFIX: rename & replace any "good" packs from when they were manually loaded.
            //foreach (mod_xyzzy_card q in localData.questions.Where(x => x.category == " Image1").ToList()) { q.category = "Image1"; }
            //foreach (mod_xyzzy_card a in localData.answers.Where(x => x.category == " Image1").ToList()) { a.category = "Image1"; }
            //localData.packs.RemoveAll(x => x.name == " Image1");
            //not required now. Need to do a proper migration script at some point for merging / deleting packs. 
            //foreach (chat c in Roboto.Settings.chatData)
            //{
            //    mod_xyzzy_chatdata chatData = (mod_xyzzy_chatdata)c.getPluginData(typeof(mod_xyzzy_chatdata));
            //    if (chatData != null)
            //    {
            //        //if (chatData.packFilter.Contains("Base") || chatData.packFilter.Contains(" Base")) { chatData.packFilter.Add("Cards Against Humanity"); }
            //        //if (chatData.packFilter.Contains("CAHe1") || chatData.packFilter.Contains(" CAHe1")) { chatData.packFilter.Add("Expansion 1 - CAH"); }
            //        //if (chatData.packFilter.Contains("CAHe2") || chatData.packFilter.Contains(" CAHe2")) { chatData.packFilter.Add("Expansion 2 - CAH"); }
            //        //if (chatData.packFilter.Contains("CAHe3") || chatData.packFilter.Contains(" CAHe3")) { chatData.packFilter.Add("Expansion 3 - CAH"); }
            //        //if (chatData.packFilter.Contains("CAHe4") || chatData.packFilter.Contains(" CAHe4")) { chatData.packFilter.Add("Expansion 4 - CAH"); }
            //        //if (chatData.packFilter.Contains("CAHe5") || chatData.packFilter.Contains(" CAHe5")) { chatData.packFilter.Add("CAH Fifth Expansion"); }
            //        //if (chatData.packFilter.Contains("CAHe6") || chatData.packFilter.Contains(" CAHe6")) { chatData.packFilter.Add("CAH Sixth Expansion"); }
            //        if (chatData.packFilter.Contains(" Image1")) { chatData.packFilter.Add("Image1"); }

            //        chatData.packFilter.RemoveAll(x => x == " Image1");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "Base");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe1");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe2");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe3");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe4");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe5");
            //        //chatData.packFilter.RemoveAll(x => x.Trim() == "CAHe6");
            //    }
            //}


            //sync anything that needs it
            localData.packSyncCheck();

            //Check for null-IDd packs and report
            List<Helpers.cardcast_pack> nullIDPacks = localData.packs.Where(x => string.IsNullOrEmpty(x.packCode)).ToList();
            Roboto.log.log("There are " + nullIDPacks.Count() + " packs without pack codes." +
                (nullIDPacks.Count() == 0 ? "": " Try rename an existing pack in the XML file to the same name, or add the pack code to this pack - should merge in next time a Sync is called. " )
                , nullIDPacks.Count() > 0?logging.loglevel.critical:  logging.loglevel.normal);
            foreach (Helpers.cardcast_pack pack in nullIDPacks)
            {
                Roboto.log.log("Pack " + pack.name + " has no pack code ", logging.loglevel.critical);
            }

            //AT THIS POINT THE GAME WILL START PROCESSING INSTRUCTIONS!!!
            //DONT GO PAST IN STARTUP TEST MODE
            //----------------------------
            int ABANDONALLHOPE = 1;
            ABANDONALLHOPE++;
            //----------------------------

        }

    }
}
