import { Component, Input } from '@angular/core';
import { CellComponent } from '../cell/cell.component';
import { MoveResultMessage, StartMessage, EndMessage, PlayerSide } from '../models';
import { WebSocketService } from '../web-socket.service';

export class Cell {
  value = '';
  row = 0;
  col = 0;

  constructor(row: number, col: number) {
    this.row = row;
    this.col = col;
  }
}

export class Row {
  row = 0;
  cells: Cell[] = [];

  constructor(row: number, cols: number) {
    this.row = row;
    for (let col = 0; col < cols; col++) {
      this.cells.push(new Cell(row, col));
    }
  }
}

@Component({
  selector: 'app-board',
  templateUrl: './board.component.html',
  styleUrls: ['./board.component.css'],
  imports: [
    CellComponent,
  ],
})
export class BoardComponent {
  @Input() rows: Row[] = [];
  @Input() moveResult: MoveResultMessage | undefined;
  @Input() startMessage: StartMessage | undefined;
  @Input() endMessage: EndMessage | undefined;

  @Input() playerSide: PlayerSide | undefined;
  @Input() yourTurn: boolean = false;

  constructor(private webSocketService: WebSocketService) { }

  ngOnInit() {
    this.initializeBoard();
    this.webSocketService.moveResultReceived.subscribe((message: MoveResultMessage) => {
      console.log('MoveResultMessage received in BoardComponent:', message);
      this.updateBoard(message);
    });

    this.webSocketService.startReceived.subscribe((message: StartMessage) => {
      console.log('StartMessage received in BoardComponent:', message);
      this.playerSide = message.side;
      this.yourTurn = message.yourTurn;
      this.initializeBoard();
    });

    this.webSocketService.endReceived.subscribe((message: EndMessage) => {
      console.log('EndMessage received in BoardComponent:', message);
      // Handle game end
    });
  }

  // TODO remove?
  ngOnChanges() {
    if (this.moveResult) {
      // Update board based on move result
      const cell = this.rows[this.moveResult.x].cells[this.moveResult.y];
      cell.value = this.moveResult.yourTurn
        ? this.playerSide === PlayerSide.X
          ? 'O'
          : 'X'
        : this.playerSide === PlayerSide.X
          ? 'X'
          : 'O';
    }

    if (this.startMessage) {
      // Handle game start
      console.log(`Game started. Player side: ${this.playerSide}, Your turn: ${this.yourTurn}`);
      this.initializeBoard();
    }

    if (this.endMessage) {
      // Handle game end
    }
  }

  private initializeBoard() {
    const numRows = 50;
    const numCols = 50;
    this.rows = [];
    for (let row = 0; row < numRows; row++) {
      this.rows.push(new Row(row, numCols));
    }
  }

  private updateBoard(message: MoveResultMessage) {
    if (!message.success)
      return;

    const cell = this.rows[message.x].cells[message.y];
    this.yourTurn = message.yourTurn;

    cell.value = message.yourTurn
      ? this.playerSide === PlayerSide.X
        ? 'O'
        : 'X'
      : this.playerSide === PlayerSide.X
        ? 'X'
        : 'O';
  }
}
