namespace JiNet.Preview
{
    public struct Const<T>
    {
        public T Value { get; private set; }

        public Const(T value) : this()
        {
            this.Value = value;
        }
    }    
}

