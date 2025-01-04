import { Component, Input } from '@angular/core';
import { WebSocketService } from '../web-socket.service';
import { MoveMessage, PlayerSide } from '../models';

@Component({
  selector: 'app-cell',
  templateUrl: './cell.component.html',
  styleUrls: ['./cell.component.css']
})
export class CellComponent {
  @Input() value: string = '';
  @Input() row: number = 0;
  @Input() col: number = 0;
  @Input() yourTurn: boolean = false;
  @Input() playerSide: PlayerSide | undefined;

  constructor(private webSocketService: WebSocketService) { }

  onCellClick() {
    console.log(`Cell clicked: ${this.row}, ${this.col}, yourTurn: ${this.yourTurn}, value: ${this.value}`);

    if (this.yourTurn && this.value === '') {
      console.log(`Cell clicked: ${this.row}, ${this.col}`);
      const moveMessage = new MoveMessage(this.row, this.col);
      this.webSocketService.sendMessage(moveMessage);
    } else {
      console.log('Not your turn');
    }
  }
}
