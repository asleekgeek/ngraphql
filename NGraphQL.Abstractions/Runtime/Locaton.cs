﻿namespace NGraphQL.Runtime {

  public class Location {
    public int Line;
    public int Column;

    public static readonly Location StartLocation = new Location() { Line = 1, Column = 1 };
    public override string ToString() => $"({Line}, {Column})";
  }

}