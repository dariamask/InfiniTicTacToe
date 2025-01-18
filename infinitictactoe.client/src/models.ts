export enum MessageType {
  ClientHello = 'clientHello',
  ServerHello = 'serverHello',
  Ready = 'ready',
  Start = 'start',
  Move = 'move',
  End = 'end',
  MoveResult = 'moveResult',
}

export enum PlayerSide {
  X = 'x',
  O = 'o',
}

export class TypedMessage {
  type: MessageType;

  constructor(type: MessageType) {
    this.type = type;
  }
}

export class ClientHelloMessage extends TypedMessage {
  nickname: string;

  constructor(nickname: string) {
    super(MessageType.ClientHello);
    this.nickname = nickname;
  }
}

export class ServerHelloMessage extends TypedMessage {
  constructor() {
    super(MessageType.ServerHello);
  }
}

export class ReadyMessage extends TypedMessage {
  constructor() {
    super(MessageType.Ready);
  }
}

export class MoveMessage extends TypedMessage {
  x: number;
  y: number;

  constructor(x: number, y: number) {
    super(MessageType.Move);
    this.x = x;
    this.y = y;
  }
}

export class StartMessage extends TypedMessage {
  side: PlayerSide;
  yourTurn: boolean;
  nicknameX: string;
  nicknameO: string;

  constructor(side: PlayerSide, yourTurn: boolean, nicknameX: string, nicknameO: string) {
    super(MessageType.Start);
    this.side = side;
    this.yourTurn = yourTurn;
    this.nicknameX = nicknameX;
    this.nicknameO = nicknameO;
  }
}

export class ClientEndMessage extends TypedMessage {
  constructor() {
    super(MessageType.Ready);
  }
}

export class EndMessage extends TypedMessage {
  scoreX: number;
  scoreO: number;

  constructor(scoreX: number, scoreO: number) {
    super(MessageType.End);
    this.scoreX = scoreX;
    this.scoreO = scoreO;
  }
}

/* Example:
{
    "success": true,
    "message": "Move accepted.",
    "x": 10,
    "y": 22,
    "scoreX": 1,
    "scoreO": 1,
    "crossedOutCells": [
        {
            "x": 11,
            "y": 22,
            "side": "o",
            "turnNumber": null,
            "crossedOut": true,
            "symbol": "O"
        },
        
    ],
    "yourTurn": true,
    "type": "moveResult"
}
 */
export class MoveResultMessage extends TypedMessage {
  success: boolean;
  message: string;
  x: number;
  y: number;
  scoreX: number;
  scoreO: number;
  yourTurn: boolean;
  crossedOutCells: CrossedOutCell[];

  constructor(
    success: boolean,
    message: string,
    x: number,
    y: number,
    scoreX: number,
    scoreO: number,
    crossedOutCells: CrossedOutCell[],
    yourTurn: boolean
  ) {
    super(MessageType.MoveResult);
    this.success = success;
    this.message = message;
    this.x = x;
    this.y = y;
    this.scoreX = scoreX;
    this.scoreO = scoreO;
    this.crossedOutCells = crossedOutCells;
    this.yourTurn = yourTurn;
  }
}

export class CrossedOutCell {
  x: number;
  y: number;
  side: PlayerSide;
  turnNumber: number | null;
  crossedOut: boolean;
  symbol: string;

  constructor(
    x: number,
    y: number,
    side: PlayerSide,
    turnNumber: number | null,
    crossedOut: boolean,
    symbol: string
  ) {
    this.x = x;
    this.y = y;
    this.side = side;
    this.turnNumber = turnNumber;
    this.crossedOut = crossedOut;
    this.symbol = symbol;
  }
}
