export enum MessageType {
  ClientHello = 'clientHello',
  ServerHello = 'serverHello',
  Move = 'move',
  Start = 'start',
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

  constructor(side: PlayerSide, yourTurn: boolean) {
    super(MessageType.Start);
    this.side = side;
    this.yourTurn = yourTurn;
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

export class MoveResultMessage extends TypedMessage {
  success: boolean;
  message: string;
  x: number;
  y: number;
  scoreX: number;
  scoreO: number;
  yourTurn: boolean;

  constructor(
    success: boolean,
    message: string,
    x: number,
    y: number,
    scoreX: number,
    scoreO: number,
    yourTurn: boolean
  ) {
    super(MessageType.MoveResult);
    this.success = success;
    this.message = message;
    this.x = x;
    this.y = y;
    this.scoreX = scoreX;
    this.scoreO = scoreO;
    this.yourTurn = yourTurn;
  }
}
