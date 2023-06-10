class Message
{
    string sender_id;
    string receiver_id;
    string message;
    public Message(string sender_id, string receiver_id, string message)
    {
        this.sender_id = sender_id;
        this.receiver_id = receiver_id;
        this.message = message;
    }
}