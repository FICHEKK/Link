using Link.Channels;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class SequenceNumberTests
{
    [TestCase(0, 0, ExpectedResult = false)]
    [TestCase(0, 1, ExpectedResult = false)]
    [TestCase(1, 0, ExpectedResult = true)]
    [TestCase(0, ushort.MaxValue, ExpectedResult = true)]
    [TestCase(ushort.MaxValue / 2, ushort.MaxValue, ExpectedResult = true)]
    [TestCase(ushort.MaxValue / 2 + 1, ushort.MaxValue, ExpectedResult = false)]
    [TestCase(ushort.MaxValue / 2, 0, ExpectedResult = true)]
    [TestCase(ushort.MaxValue / 2 + 1, 0, ExpectedResult = false)]
    public bool Is_first_sequence_number_greater(int seq1, int seq2) =>
        Channel.IsFirstSequenceNumberGreater((ushort) seq1, (ushort) seq2);
}
