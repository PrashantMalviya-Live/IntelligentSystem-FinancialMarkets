export class AlgoHealth {
  constructor(
    //algo instance id
    public aIns: number,

    //algo health = true : running ; false: stopped
    public ah: boolean,

    //last updated time for health
    public ad: number
  ) { }
}
